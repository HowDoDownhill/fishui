shader_type canvas_item;

// Frost comes from the screen texture's mipmap chain. Each tap reads a level whose texels are about
// as wide as the gap between taps, so a few taps cover the whole blur disk and the panel comes out
// smooth on a sharp display and through a video encoder alike.
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
// Blur radius in screen pixels. Larger = only broad color survives, no shape.
uniform float blur_radius : hint_range(0.0, 150.0) = 45.0;
uniform vec4 mix_color : source_color = vec4(0.0, 0.0, 0.0, 0.4);
// Minimum luminance the finished panel is allowed to sit at. Both the blurred backdrop and the
// theme tint are dark, so over dark content the panel collapses toward black and text on it stops
// reading. Lifting to a floor (rather than adding a flat brightness) leaves panels over bright
// content untouched, which is the same job Acrylic's luminosity layer does.
uniform float luminosity_floor : hint_range(0.0, 1.0) = 0.0;
uniform int sample_count : hint_range(1, 64) = 16;
// NOTE: panel edge treatment is NOT drawn here. Anything reconstructed from UV derivatives ends up
// ~1px asymmetric (top/left vs bottom/right) because of sub-pixel rasterization, and this shader
// forces alpha to 1.0 so a stylebox shadow drawn by the panel itself would render as an opaque
// rectangle. Instead each mica panel gets a native StyleBoxFlat drop shadow on a ShowBehindParent
// child (see MicaShadow.cs), which follows the panel's own rounded corners.

float dither(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

void fragment() {
    // Derive texel size from the screen texture itself. SCREEN_PIXEL_SIZE is unreliable (often 0)
    // in canvas shaders under the gl_compatibility renderer, which collapses the blur to nothing.
    vec2 texel = 1.0 / vec2(textureSize(screen_texture, 0));
    vec2 px = texel * blur_radius;

    // Taps on a disk of area pi * r^2 sit about r * sqrt(pi / n) apart. Reading the level whose
    // texels are that wide lets neighbouring taps overlap, so nothing sharp falls between them.
    float spacing = blur_radius * sqrt(3.14159265 / float(sample_count));
    float lod = max(0.0, log2(max(spacing, 1.0)));

    vec4 acc = vec4(0.0);
    float total = 0.0;

    // Golden-angle spiral covering the disk evenly, weighted so the blur falls off toward its rim.
    for (int i = 0; i < sample_count; i++) {
        float t = (float(i) + 0.5) / float(sample_count);
        float r = sqrt(t);                 // uniform disk distribution
        float a = float(i) * 2.399963;     // golden angle
        float weight = exp(-2.0 * t);
        acc += textureLod(screen_texture, SCREEN_UV + vec2(cos(a), sin(a)) * r * px, lod) * weight;
        total += weight;
    }

    vec4 blurred = acc / total;

    // Tint the frosted result toward the theme color by its alpha, then force the panel fully
    // opaque. If we let the panel stay semi-transparent, the SHARP (unblurred) background bleeds
    // through via normal alpha blending and visually cancels the frost — the panel must be opaque
    // so the only thing seen is the blurred screen + tint.
    vec3 frosted = mix(blurred.rgb, mix_color.rgb, mix_color.a);

    // Applied after the tint so the floor is a guarantee about the final panel, not an input the
    // tint can undo.
    float lum = dot(frosted, vec3(0.2126, 0.7152, 0.0722));
    frosted += max(0.0, luminosity_floor - lum);

    // Half an 8-bit step of fixed dither, so the slow gradient a wide blur leaves does not band.
    frosted += (dither(FRAGCOORD.xy) - 0.5) / 255.0;

    COLOR = vec4(frosted, 1.0);
}
