#version 300 es
// Application-owned optics: one capture, a window bevel and four control lenses.
precision highp float;
precision highp sampler2D;
in vec2 uv;
out vec4 out_color;
uniform vec2 u_resolution;
uniform sampler2D u_textures[16];
uniform vec4 button0;
uniform vec4 button1;
uniform vec4 button2;
uniform vec4 button3;
uniform float dpiScale;

// Distance and outward normal of a rounded rectangle, evaluated in pixels.
vec3 boundary(vec2 p, vec4 box, float r) {
    vec2 v = p - box.xy - box.zw * 0.5;
    vec2 q = abs(v) - (box.zw * 0.5 - r);
    vec2 outside = max(q, 0.0);
    float len = length(outside);
    vec2 n = len > 0.0001 ? outside / len :
        (q.x > q.y ? vec2(1.0, 0.0) : vec2(0.0, 1.0));
    n *= sign(v);
    return vec3(n, len + min(max(q.x, q.y), 0.0) - r);
}

// A convex bevel bends rays most at the rim and joins the flat centre smoothly.
vec2 bend(vec2 p, vec4 box, float radius, float band, float depth,
          inout vec2 dispersion, inout float shine) {
    vec3 edge = boundary(p, box, radius);
    float inside = 1.0 - smoothstep(-0.8, 0.5, edge.z);
    float t = clamp(-edge.z / band, 0.0, 1.0);
    float slope = (1.0 - t) * (1.0 - t);
    float profile = slope * inside;
    dispersion += edge.xy * profile * 0.32 * dpiScale;
    float rim = exp(-abs(edge.z + 0.6) / (0.95 * dpiScale));
    shine += rim * inside * (0.025 + 0.065 * max(dot(edge.xy, normalize(vec2(-0.6, -1.0))), 0.0));
    return p - edge.xy * depth * profile;
}

vec3 sampleDesktop(vec2 pixel) {
    vec2 pos = pixel / u_resolution;
    pos.y = 1.0 - pos.y;
    pos = clamp(pos, vec2(0.001), vec2(0.999));
    return texture(u_textures[0], pos).rgb;
}

void main() {
    // Layout coordinates start at the top; the capture texture uses WebGL UVs.
    vec2 p = vec2(uv.x, 1.0 - uv.y) * u_resolution;
    vec4 shell = vec4(0.0, 0.0, u_resolution);
    float sd = boundary(p, shell, 32.0 * dpiScale).z;
    float alpha = 1.0 - smoothstep(-0.8, 0.5, sd);
    if (alpha <= 0.0) { out_color = vec4(0.0); return; }
    vec2 dispersion = vec2(0.0);
    float shine = 0.0;
    vec2 ray = p;
    // Buttons and their animated chrome use the exact same measured geometry.
    ray = bend(ray, button0, button0.w * 0.5, button0.w * 0.42, 9.0 * dpiScale, dispersion, shine);
    ray = bend(ray, button1, button1.w * 0.5, button1.w * 0.42, 9.0 * dpiScale, dispersion, shine);
    ray = bend(ray, button2, button2.w * 0.5, button2.w * 0.42, 6.0 * dpiScale, dispersion, shine);
    ray = bend(ray, button3, button3.w * 0.5, button3.w * 0.42, 6.0 * dpiScale, dispersion, shine);
    ray = bend(ray, shell, 32.0 * dpiScale, 46.0 * dpiScale, 26.0 * dpiScale, dispersion, shine);
    // A wider Gaussian kernel softens desktop detail without blurring WPF controls.
    vec3 color = vec3(0.0);
    float totalWeight = 0.0;
    for (int y = -3; y <= 3; y++) {
        for (int x = -3; x <= 3; x++) {
            vec2 offset = vec2(float(x), float(y));
            float weight = exp(-dot(offset, offset) / 5.12);
            color += sampleDesktop(ray + offset * 3.0 * dpiScale) * weight;
            totalWeight += weight;
        }
    }
    color /= totalWeight;
    color.r = mix(color.r, sampleDesktop(ray - dispersion).r, 0.08);
    color.b = mix(color.b, sampleDesktop(ray + dispersion).b, 0.08);
    out_color = vec4(clamp(color * 0.70 + shine, 0.0, 1.0) * alpha, alpha);
}
