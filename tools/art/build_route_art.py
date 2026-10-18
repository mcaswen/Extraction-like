"""Reproducible Blender source for collision-free route equipment.
Run: blender -b --python tools/art/build_route_art.py -- /absolute/project
Generated FBX assets use metres; the Unity installer fits the recovery cases to existing visuals.
"""
import bpy, math, sys, os, json
from mathutils import Vector, Quaternion

ROOT = os.path.abspath(sys.argv[sys.argv.index('--') + 1])
OUT = os.path.join(ROOT, 'Assets/Art/Environment/AerospaceRouteNetwork/Models')
SOURCE = os.path.join(ROOT, 'SourceArt/AerospaceRouteNetwork')
os.makedirs(OUT, exist_ok=True)
os.makedirs(SOURCE, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
MATS={}
for name,color,metal,rough in [
    ('Ceramic',(.72,.79,.81,1),.08,.32),('Graphite',(.032,.059,.076,1),.32,.4),
    ('Alloy',(.32,.43,.49,1),.78,.26),('Orange',(.94,.25,.035,1),.08,.33),
    ('Signal',(.10,.73,.94,1),.1,.28),('Solar',(.018,.10,.22,1),.5,.24)]:
    mat=bpy.data.materials.new('Route_'+name); mat.diffuse_color=color; mat.use_nodes=True
    p=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'); p.inputs['Base Color'].default_value=color
    p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough
    if name=='Signal': p.inputs['Emission Color'].default_value=color; p.inputs['Emission Strength'].default_value=1.4
    MATS[name]=mat
CURRENT=[]

def finish(o,name,mat,bevel=0):
    o.name=name; o.data.materials.append(MATS[mat]); CURRENT.append(o)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        m=o.modifiers.new('Machined edge','BEVEL');m.width=bevel;m.segments=2
        bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=m.name)
    for p in o.data.polygons:p.use_smooth=True
    n=o.modifiers.new('Face weighted normals','WEIGHTED_NORMAL');n.keep_sharp=True;n.weight=45
    bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=n.name)
    return o

def box(name,p,s,mat='Ceramic',bevel=.035,rot=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.scale=s
    if rot:o.rotation_euler=rot
    return finish(o,name,mat,bevel)

def cyl(name,p,r,d,mat='Alloy',vertices=24,direction=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=d,location=p);o=bpy.context.object
    if direction:o.rotation_euler=Vector(direction).to_track_quat('Z','Y').to_euler()
    return finish(o,name,mat,min(.018,r*.08))

def beam(name,a,b,r,mat='Alloy',vertices=12):
    va,vb=Vector(a),Vector(b); return cyl(name,(va+vb)/2,r,(vb-va).length,mat,vertices,vb-va)

def ring(name,p,r,t,mat='Alloy',rotate=None):
    bpy.ops.mesh.primitive_torus_add(major_radius=r,minor_radius=t,major_segments=32,minor_segments=8,location=p)
    o=bpy.context.object
    if rotate:o.rotation_euler=rotate
    return finish(o,name,mat)

def lathe(name,profile,mat='Alloy',segments=40):
    vs=[(r*math.cos(i*2*math.pi/segments),r*math.sin(i*2*math.pi/segments),z) for r,z in profile for i in range(segments)]
    fs=[]
    for j in range(len(profile)-1):
        for i in range(segments):fs.append((j*segments+i,j*segments+(i+1)%segments,(j+1)*segments+(i+1)%segments,(j+1)*segments+i))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vs,[],fs);mesh.update()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active=o;o.select_set(True);return finish(o,name,mat)

def rocket_mark(p,size=1):
    # A broad aerospace pictogram, legible from the fixed high game camera.
    x,y,z=p
    verts=[(-.10,-.32),(.10,-.32),(.10,.18),(0,.42),(-.10,.18),(-.10,-.08),(-.27,-.32),(-.27,-.48),(-.10,-.32),(.10,-.08),(.27,-.32),(.27,-.48),(.10,-.32)]
    verts=[(x+a*size,y+b*size,z) for a,b in verts]
    mesh=bpy.data.meshes.new('Recovery insignia');mesh.from_pydata(verts,[],[(0,1,2,3,4),(5,6,7,8),(12,11,10,9)]);mesh.update()
    o=bpy.data.objects.new('Rocket recovery insignia',mesh);bpy.context.collection.objects.link(o);finish(o,o.name,'Ceramic')

def recovery_case():
    # A real cavity, not a solid block hidden beneath an animated lid.
    box('Pressure floor',(0,0,.34),(1.52,2.28,.30),'Graphite',.08)
    for x in [-.70,.70]:
        box('Pressure side wall',(x*.97,0,.86),(.12,2.28,1.00),'Graphite',.045)
        box('Sealing rim',(x,0,1.31),(.15,2.31,.12),'Graphite',.035)
        box('Interior locator rail',(x*.85,0,.64),(.024,1.83,.07),'Signal',.008)
    for y in [-1.08,1.08]:
        box('Pressure end wall',(0,y,.86),(1.28,.12,1.00),'Graphite',.045)
        box('Sealing end rim',(0,y,1.31),(1.25,.15,.12),'Graphite',.035)
    box('Recessed equipment tray',(0,0,.48),(1.21,1.94,.12),'Alloy',.06)
    for y in [-.59,0,.59]:
        box('Cushioned sample recess',(0,y,.56),(1.08,.46,.075),'Graphite',.065)
        box('Sealed transport canister',(0,y,.69),(.78,.30,.21),'Ceramic',.07)
        box('Canister grip',(0,y,.805),(.33,.12,.025),'Alloy',.01)
        box('Canister locator',(.29,y,.805),(.05,.15,.026),'Signal',.006)
    def lid(o):
        o['articulation']='Lid'
        return o
    lid(box('Chamfered service lid',(0,0,1.48),(1.51,2.27,.25),'Ceramic',.095))
    lid(box('Lid inner liner',(0,0,1.348),(1.20,1.96,.035),'Graphite',.07))
    for x in [-.48,.48]:
        lid(box('Lid reinforcement',(x,0,1.317),(.075,1.73,.035),'Alloy',.016))
        lid(box('Lid inspection strip',(x*.77,0,1.313),(.027,1.43,.025),'Signal',.006))
    for x in [-.47,.47]:
        cyl('Captive hinge barrel',(x,-1.075,1.30),.075,.27,'Alloy',20,(1,0,0))
    box('Lower armor',(0,0,.22),(1.56,2.32,.25),'Alloy',.07)
    for x in [-.64,.64]:
        lid(box('Upper spine',(x,0,1.64),(.14,2.15,.10),'Alloy',.027))
        for y in [-.98,.98]:
            box('Shock boot',(x,y,.13),(.30,.32,.24),'Graphite',.065)
            box('Corner armor',(x,y,.81),(.24,.25,1.22),'Ceramic',.06)
            box('Corner orange ID',(x,y,1.33),(.25,.26,.12),'Orange',.018)
            lid(cyl('Captive lid screw',(x,y,1.71),.036,.023,'Graphite',12))
    for x in [-.785,.785]:
        box('Removable side plate',(x,0,.82),(.055,1.52,.72),'Ceramic',.022)
        for y in [-.56,.56]:
            for o in [box('Overcenter lock',(x*1.015,y,1.28),(.085,.17,.35),'Alloy',.02),
                      box('Lock finger',(x*1.06,y,1.28),(.045,.06,.18),'Graphite',.009)]:
                o['articulation']='LockLeft' if x<0 else 'LockRight'
        box('Recessed handle',(x*1.04,0,1.02),(.055,.62,.19),'Graphite',.022)
        beam('Grip',(x*1.08,-.23,1.04),(x*1.08,.23,1.04),.037)
        for y in [-.4,-.2,0,.2,.4]:box('Lower cooling rib',(x*1.017,y,.60),(.04,.08,.22),'Alloy',.008)
    for y in [-1.15,1.15]:
        box('Face armor',(0,y,.82),(1.02,.045,.72),'Ceramic',.04)
        box('Interface bezel',(0,y*1.025,1.01),(.66,.07,.27),'Graphite',.025)
        box('Blue condition window',(0,y*1.063,1.01),(.40,.012,.065),'Signal',.007)
        for x in [-.43,.43]:cyl('Pressure port',(x,y*1.045,.68),.07,.04,'Alloy',16,(0,1,0))
    lid(box('Top ID field',(0,0,1.616),(.93,1.24,.024),'Graphite',.06))
    rocket_mark((0,.03,1.634),1.15)
    lid(CURRENT[-1])
    lid(box('Orange registration bar',(0,-.83,1.624),(.91,.15,.02),'Orange',.015))
    for i in range(3): lid(box('Charge tick',(-.20+i*.20,.87,1.63),(.12,.055,.025),'Signal',.008))

def solar_relay():
    box('Ballast base',(0,0,.18),(2.7,2.25,.36),'Graphite',.13)
    for x in [-1.16,1.16]:box('Anchor skid',(x,0,.14),(.22,2.7,.25),'Alloy')
    box('Avionics enclosure',(0,0,1.0),(1.5,1.0,1.4),'Ceramic',.13)
    box('Access door',(0,-.53,1.07),(1.15,.08,.93),'Graphite',.055)
    box('Status window',(0,-.58,1.37),(.78,.025,.12),'Signal',.012)
    box('Safety stripe',(0,-.58,.77),(.80,.02,.12),'Orange',.012)
    for x in [-.43,.43]:cyl('Door fixing',(x,-.59,1.05),.045,.025,'Alloy',12,(0,1,0))
    cyl('Telescopic mast',(0,.19,2.7),.13,3.2)
    for z in [1.6,2.8,4.15]:cyl('Mast collar',(0,.19,z),.20,.16,'Graphite')
    for a in [0,2.094,4.188]:beam('Tripod brace',(math.sin(a)*1.12,math.cos(a)*.90,.35),(0,.19,2),.055)
    box('Receiver',(0,.19,4.55),(.72,.45,.72),'Ceramic',.08)
    box('Receiver face',(0,-.05,4.55),(.44,.02,.42),'Graphite',.03)
    for x in [-.56,.56]:beam('Dipole',(x,.19,4.25),(x,.19,5.35),.035)
    beam('Dipole bar',(-.56,.19,4.55),(.56,.19,4.55),.06)
    box('Mast cap',(0,.19,4.95),(.36,.32,.12),'Orange',.035)
    # Raised rigid photovoltaic wings keep the ground footprint compact.
    for sign in [-1,1]:
        box('PV backing',(sign*1.43,.12,2.18),(1.10,2.0,.12),'Alloy',.04)
        for i in range(3):
            for j in range(5):box('Silicon cell',(sign*1.43+(i-1)*.33,.12+(j-2)*.37,2.254),(.30,.335,.024),'Solar',.014)
        beam('PV cantilever',(sign*.10,.12,1.8),(sign*1.7,.12,2.12),.065)

def engine_cradle():
    box('Transport skid',(0,0,.24),(3.3,3.6,.45),'Graphite',.11)
    for x in [-1.42,1.42]:
        box('Orange rail',(x,0,.50),(.22,3.35,.16),'Orange',.025)
        for y in [-1.35,1.35]:box('Shock isolator',(x,y,.67),(.35,.45,.30),'Alloy',.045)
    profile=[(.91,.59),(.89,.70),(.78,1.02),(.62,1.42),(.46,1.87),(.39,2.23),(.40,2.5),(.48,2.64),(.48,2.78),(.39,2.78),(.31,2.5),(.31,2.23),(.39,1.87),(.55,1.42),(.71,1.02),(.82,.70),(.84,.59),(.91,.59)]
    lathe('Recovered engine bell',profile)
    for z,r in [(.66,.92),(1.05,.79),(2.4,.41),(2.7,.48)]:ring('Cooling ring',(0,0,z),r,.038,'Orange' if z==2.4 else 'Alloy')
    for i in range(16):
        a=i*math.pi/8
        for (r,z),(r2,z2) in zip(profile[:7],profile[1:7]):beam('Cooling channel',(math.cos(a)*(r+.03),math.sin(a)*(r+.03),z),(math.cos(a)*(r2+.03),math.sin(a)*(r2+.03),z2),.018,'Alloy',6)
    cyl('Closed protective cap',(0,0,2.88),.55,.20,'Ceramic')
    for a in [0,2.094,4.188]:beam('Transport brace',(math.cos(a)*1.35,math.sin(a)*1.45,.6),(math.cos(a)*.45,math.sin(a)*.45,2.42),.075,'Graphite')
    box('Inventory plate',(0,-1.72,.68),(1.3,.08,.28),'Ceramic',.035)
    box('Locator signal',(0,-1.773,.68),(.65,.02,.06),'Signal',.008)

def survey_beacon():
    box('Sensor ballast',(0,0,.16),(1.15,1.1,.30),'Graphite',.09)
    box('Survey post',(0,0,1.15),(.55,.48,1.85),'Ceramic',.08)
    box('Dark inset',(0,-.255,1.23),(.34,.035,1.4),'Graphite',.04)
    box('Photonic locator',(0,-.28,1.68),(.15,.025,.46),'Signal',.015)
    box('Unit badge',(0,-.28,.80),(.24,.02,.20),'Orange',.01)
    cyl('Gimbal azimuth',(0,0,2.13),.32,.18)
    box('Optics head',(0,0,2.4),(.78,.55,.42),'Graphite',.09)
    for x in [-.19,.19]:
        cyl('Lens surround',(x,-.3,2.4),.13,.09,'Alloy',24,(0,1,0))
        cyl('Lens',(x,-.353,2.4),.085,.025,'Signal',20,(0,1,0))

stats={}
for name,build in [('RecoveryCase',recovery_case),('SolarRelay',solar_relay),('EngineCradle',engine_cradle),('SurveyBeacon',survey_beacon)]:
    CURRENT=[];build(); objects=CURRENT[:]
    col=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(col)
    for o in objects:
        for old in list(o.users_collection):old.objects.unlink(o)
        col.objects.link(o)
    # Collapse by material for a bounded renderer count, and export genuine geometric LODs.
    groups=[]
    # Articulated parts remain separate, while each part still batches by shared material.
    batches=[(part,mat,[o for o in objects if o.data.materials[0]==mat and o.get('articulation','Body')==part])
             for part in (['Body','Lid','LockLeft','LockRight'] if name=='RecoveryCase' else ['Body']) for mat in MATS.values()]
    for part,mat,obs in batches:
        if not obs:continue
        bpy.ops.object.select_all(action='DESELECT')
        for o in obs:o.select_set(True)
        bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();o=bpy.context.object
        o.name=name+('_'+part if name=='RecoveryCase' else '')+'_'+mat.name; bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');groups.append(o)
    stats[name]=[]
    for level,ratio in enumerate([1,.40,.14]):
        exports=[]
        for original in groups:
            o=original.copy();o.data=original.data.copy();bpy.context.collection.objects.link(o);exports.append(o)
            # Preserve thin labels, solar cells and signal lenses at every distance.
            preserve=original.data.materials[0].name in ['Route_Signal','Route_Solar'] or (name=='RecoveryCase' and original.data.materials[0].name=='Route_Ceramic')
            if ratio<1 and not preserve:
                bpy.context.view_layer.objects.active=o
                mod=o.modifiers.new('Screen-space LOD','DECIMATE');mod.ratio=ratio
                bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.ops.object.select_all(action='DESELECT')
        for o in exports:o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,name+'_LOD'+str(level)+'.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True)
        tris=0
        for o in exports:o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles)
        stats[name].append(tris)
        for o in exports:bpy.data.objects.remove(o,do_unlink=True)
    col.hide_render=True

# Keep an editable source collection for each asset; no scene scaffolding goes into FBX.
for col in bpy.data.collections:
    if col.name in stats:col.hide_render=False
for i,name in enumerate(stats):
    for obj in bpy.data.collections[name].objects:obj.location.x+=(i-1.5)*6
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.color_type='MATERIAL'
            area.spaces.active.region_3d.view_distance=21
            area.spaces.active.region_3d.view_location=(0,0,1.5)
            area.spaces.active.region_3d.view_rotation=Quaternion((.84,.49,.12,.20)).normalized()
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'AerospaceRouteEquipment.blend'))
with open(os.path.join(SOURCE,'mesh-statistics.json'),'w') as f:json.dump(stats,f,indent=2)
print('ROUTE_ART_MODELS',stats)
