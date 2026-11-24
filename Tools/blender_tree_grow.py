# ไฟล์นี้: [Tools] สร้างแอนิเมชัน "ต้นไม้โต" แบบการ์ตูน ด้วย Blender (ไม่ต้องเปิดหน้าต่าง Blender)
#   ต้นกล้า -> ลำต้นยืดและหนาขึ้น -> แตกกิ่ง -> พุ่มใบเด้งออกมาเป็นก้อน ๆ (สไตล์เดียวกับไอคอนต้นไม้ใน HUD)
#   เรนเดอร์พื้นโปร่งใส (RGBA) ออกเป็น PNG ทีละเฟรม แล้ว make_tree_video.bat จะรวมเป็น .mov / sprite sheet
# รัน: blender -b -P Tools/blender_tree_grow.py -- <โฟลเดอร์ผลลัพธ์> [จำนวนเฟรม] [ขนาดภาพ]
import bpy, math, random, sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else "//tree_frames"
FRAMES = int(argv[1]) if len(argv) > 1 else 96
RES = int(argv[2]) if len(argv) > 2 else 512
random.seed(7)

# ---------------------------------------------------------------- ฉากว่าง
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.frame_start, scene.frame_end = 1, FRAMES
scene.render.fps = 30
for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
    try:
        scene.render.engine = engine
        break
    except TypeError:
        pass
scene.render.film_transparent = True
scene.render.resolution_x = scene.render.resolution_y = RES
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "Standard"
scene.render.filepath = OUT.rstrip("/\\") + "/tree_"

world = bpy.data.worlds.new("World"); scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (1.0, .78, .62, 1)   # แสงโทนพระอาทิตย์ตก
world.node_tree.nodes["Background"].inputs[1].default_value = .35


def toon(name, dark, mid, light, rim=(1, .85, .55)):
    """วัสดุการ์ตูน 3 ระดับแสง (Diffuse -> Shader to RGB -> ColorRamp แบบขั้นบันได -> Emission)"""
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    diff = nt.nodes.new("ShaderNodeBsdfDiffuse")
    s2r = nt.nodes.new("ShaderNodeShaderToRGB")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    e = ramp.color_ramp.elements
    lin = lambda c: (*(v ** 2.2 for v in c), 1)          # สีที่กำหนดเป็น sRGB -> แปลงเป็น linear
    e[0].position, e[0].color = 0.0, lin(dark)
    e[1].position, e[1].color = .22, lin(mid)
    e3 = e.new(.6); e3.color = lin(light)
    e4 = e.new(.93); e4.color = lin(rim)
    emit = nt.nodes.new("ShaderNodeEmission")
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(diff.outputs[0], s2r.inputs[0])
    nt.links.new(s2r.outputs[0], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], emit.inputs[0])
    nt.links.new(emit.outputs[0], out.inputs[0])
    return m


BARK = toon("Bark", (.24, .13, .07), (.38, .22, .12), (.52, .33, .18), (.66, .45, .27))
LEAF = toon("Leaf", (.13, .38, .12), (.24, .55, .16), (.4, .72, .2), (.66, .86, .3))
LEAF2 = toon("LeafLight", (.18, .45, .13), (.32, .64, .18), (.52, .8, .24), (.78, .92, .38))


def key(obj_or_data, path, frame, value, interp="BEZIER", easing="AUTO"):
    setattr(obj_or_data, path, value)
    obj_or_data.keyframe_insert(path, frame=frame)
    ad = obj_or_data.animation_data
    if ad and ad.action:
        fcurves = getattr(ad.action, "fcurves", None)
        if fcurves is None:      # Blender 5: action -> layers -> strips -> channelbags
            fcurves = [fc for layer in ad.action.layers for strip in layer.strips
                       for bag in strip.channelbags for fc in bag.fcurves]
        for fc in fcurves:
            if fc.data_path == path:
                for kp in fc.keyframe_points:
                    if abs(kp.co.x - frame) < .01:
                        kp.interpolation = interp; kp.easing = easing


# ---------------------------------------------------------------- กิ่ง (เส้นโค้ง + ความหนาเรียวไปที่ปลาย)
def branch(points, radius, start, end, thick_end=None):
    cu = bpy.data.curves.new("Branch", "CURVE"); cu.dimensions = "3D"
    cu.bevel_depth = radius; cu.bevel_resolution = 4; cu.use_fill_caps = True
    cu.bevel_factor_mapping_end = "SPLINE"
    sp = cu.splines.new("NURBS"); sp.points.add(len(points) - 1)
    for i, p in enumerate(points):
        sp.points[i].co = (*p, 1)
        sp.points[i].radius = 1.0 - .72 * i / (len(points) - 1)
    sp.use_endpoint_u = True; sp.order_u = min(4, len(points))
    ob = bpy.data.objects.new("Branch", cu); scene.collection.objects.link(ob)
    cu.materials.append(BARK)
    key(cu, "bevel_factor_end", 1, 0.0)
    key(cu, "bevel_factor_end", start, 0.0)
    key(cu, "bevel_factor_end", end, 1.0, "SINE", "EASE_OUT")
    # กิ่งหนาขึ้นตามเวลาจนจบแอนิเมชัน
    key(cu, "bevel_depth", start, radius * .35)
    key(cu, "bevel_depth", FRAMES, thick_end or radius, "SINE", "EASE_IN_OUT")
    return ob


def wobble_line(a, b, n=5, amp=.12):
    pts = []
    for i in range(n):
        t = i / (n - 1)
        p = a.lerp(b, t)
        if 0 < i < n - 1:
            p += Vector((random.uniform(-amp, amp), random.uniform(-amp, amp) * .5, random.uniform(-amp, amp) * .4))
        pts.append(tuple(p))
    return pts


def blob(center, r, start, mat):
    """พุ่มใบ 1 ก้อน (ทรงกลมบี้นิดหน่อย) เด้งขยายจาก 0 แบบ Back easing"""
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=1, location=center)
    ob = bpy.context.active_object
    bpy.ops.object.shade_smooth()
    ob.data.materials.append(mat)
    ob.rotation_euler = (random.random(), random.random(), random.random())
    final = Vector((r, r * random.uniform(.85, 1.0), r * random.uniform(.78, .92)))
    key(ob, "scale", 1, Vector((0, 0, 0)))
    key(ob, "scale", start, Vector((0, 0, 0)))
    key(ob, "scale", min(FRAMES, start + 14), final, "BACK", "EASE_OUT")
    return ob


def leaf_pair(at, start, end, size=.35):
    """ใบอ่อน 2 ใบช่วงต้นกล้า (ทรงกลมแบน) — หดหายเมื่อพุ่มใหญ่ขึ้นมาแทน"""
    for side in (-1, 1):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=1, location=(at.x + side * size * .9, at.y, at.z + size * .2))
        ob = bpy.context.active_object; bpy.ops.object.shade_smooth()
        ob.data.materials.append(LEAF2)
        ob.rotation_euler = (0, side * -.5, 0)
        s = Vector((size, size * .45, size * .18))
        key(ob, "scale", 1, Vector((0, 0, 0)))
        key(ob, "scale", start, Vector((0, 0, 0)))
        key(ob, "scale", start + 10, s, "BACK", "EASE_OUT")
        key(ob, "scale", end, s)
        key(ob, "scale", end + 10, Vector((0, 0, 0)), "SINE", "EASE_IN")


# ---------------------------------------------------------------- ประกอบต้นไม้ตามเวลา
F = FRAMES / 96.0                       # สเกลจังหวะ (ออกแบบไว้ที่ 96 เฟรม)
f = lambda x: max(1, int(round(x * F)))

trunk_top = Vector((.05, 0, 2.3))
branch(wobble_line(Vector((0, 0, -.05)), trunk_top, 6, .1), .2, f(1), f(40), .3)
leaf_pair(Vector((0, 0, .55)), f(3), f(30))

tips = []
def grow(base, direction, length, radius, depth, start):
    end = base + direction.normalized() * length
    dur = 14 + 6 * depth
    branch(wobble_line(base, end, 4, .08 * length), radius, f(start), f(start + dur), radius * 1.4)
    tips.append((end, depth, start + dur))
    if depth >= 2:
        return
    for k in range(2 if depth else 3):
        ang = random.uniform(-.9, .9) + (k - (0.5 if depth else 1)) * .7
        d = Vector((math.sin(ang), random.uniform(-.4, .4), math.cos(ang) * .9 + .3))
        t = random.uniform(.55, .9)
        grow(base.lerp(end, t), d, length * random.uniform(.55, .7), radius * .55, depth + 1, start + dur * t + 3)

grow(Vector((0, 0, 1.25)), Vector((-.8, 0, 1)), 1.2, .09, 0, 26)
grow(Vector((.03, 0, 1.55)), Vector((.85, .1, 1)), 1.1, .085, 0, 32)
grow(Vector((.05, 0, 2.0)), Vector((.1, -.1, 1)), .9, .08, 0, 38)

# พุ่มใบ: ก้อนใหญ่ที่ปลายกิ่ง + ก้อนเติมกลางพุ่ม
crown = []
for (p, depth, t) in tips:
    if depth >= 1:
        crown.append((p, random.uniform(.55, .75), t - 4))
center = Vector((0, 0, 3.05))
for i in range(9):
    a = i / 9 * math.tau
    crown.append((center + Vector((math.cos(a) * 1.05, math.sin(a) * .5 - .15, math.sin(a * 2) * .25 + .1)),
                  random.uniform(.6, .8), 58 + i * 2.2))
crown.append((center + Vector((0, -.2, .45)), .95, 66))
crown.append((center + Vector((0, -.35, -.1)), .85, 70))
for i, (p, r, t) in enumerate(sorted(crown, key=lambda c: c[2])):
    blob(p, r, f(min(t, 84)), LEAF2 if i % 3 == 0 else LEAF)

# ---------------------------------------------------------------- กล้องและแสง
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); scene.collection.objects.link(cam)
cam.data.type = "ORTHO"; cam.data.ortho_scale = 6.2
cam.location = (0, -12, 2.75); cam.rotation_euler = (math.radians(90), 0, 0)
scene.camera = cam
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN")); scene.collection.objects.link(sun)
sun.data.energy = 3.2; sun.data.color = (1, .9, .75)
sun.rotation_euler = (math.radians(50), math.radians(-35), math.radians(-25))

bpy.ops.render.render(animation=True)
print("[tree] rendered", FRAMES, "frames ->", OUT)
