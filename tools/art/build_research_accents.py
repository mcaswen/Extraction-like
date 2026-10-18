"""Blender-built, presentation-only aerospace equipment for the three marked areas.
blender -b --python tools/art/build_research_accents.py -- /absolute/unity/project
Metres, real mesh LODs, grouped materials; no lights, collision or gameplay in exports.
"""
import bpy, math, sys, os, json
from mathutils import Vector, Quaternion

ROOT=os.path.abspath(sys.argv[sys.argv.index('--')+1])
OUT=os.path.join(ROOT,'Assets/Art/Environment/AerospaceResearchDetails/Models')
SOURCE=os.path.join(ROOT,'SourceArt/AerospaceResearchDetails')
os.makedirs(OUT,exist_ok=True);os.makedirs(SOURCE,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
MATS={}; CURRENT=[]
for key,color,metal,rough in [('Ceramic',(.72,.79,.81,1),.08,.32),('Graphite',(.032,.059,.076,1),.32,.4),
    ('Alloy',(.32,.43,.49,1),.78,.26),('Orange',(.94,.25,.035,1),.08,.33),
    ('Signal',(.10,.73,.94,1),.1,.28),('Solar',(.018,.10,.22,1),.5,.24),('Foil',(.57,.32,.075,1),.7,.40)]:
    m=bpy.data.materials.new('Route_'+key);m.diffuse_color=color;m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=color
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    if key=='Signal':p.inputs['Emission Color'].default_value=color;p.inputs['Emission Strength'].default_value=1.4
    MATS[key]=m

def finish(o,name,mat,bevel=0):
    o.name=name;o.data.materials.append(MATS[mat]);CURRENT.append(o)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Machined radii','BEVEL');mod.width=bevel;mod.segments=2
        bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in o.data.polygons:p.use_smooth=True
    mod=o.modifiers.new('Weighted hard-surface normals','WEIGHTED_NORMAL');mod.keep_sharp=True
    bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def box(name,p,s,mat='Ceramic',bevel=.035):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.scale=s
    return finish(o,name,mat,bevel)

def cyl(name,p,r,d,mat='Alloy',n=24,direction=(0,0,1)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=n,radius=r,depth=d,location=p);o=bpy.context.object
    o.rotation_euler=Vector(direction).to_track_quat('Z','Y').to_euler()
    return finish(o,name,mat,min(.016,r*.07))

def beam(name,a,b,r,mat='Alloy',n=8):
    a,b=Vector(a),Vector(b);return cyl(name,(a+b)*.5,r,(b-a).length,mat,n,b-a)

def ring(name,p,r,t,mat='Alloy',direction=(0,0,1)):
    bpy.ops.mesh.primitive_torus_add(major_radius=r,minor_radius=t,major_segments=40,minor_segments=6,location=p)
    o=bpy.context.object;o.rotation_euler=Vector(direction).to_track_quat('Z','Y').to_euler();return finish(o,name,mat)

def lathe(name,profile,mat='Ceramic',segments=48):
    vs=[(r*math.cos(i*2*math.pi/segments),r*math.sin(i*2*math.pi/segments),z) for r,z in profile for i in range(segments)]
    fs=[]
    for j in range(len(profile)-1):
        for i in range(segments):fs.append((j*segments+i,j*segments+(i+1)%segments,(j+1)*segments+(i+1)%segments,(j+1)*segments+i))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vs,[],fs);mesh.update();o=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True)
    return finish(o,name,mat)

def skid(width=3.2,length=3.5):
    for x in [-width*.43,width*.43]:
        box('Isolated transport skid',(x,0,.14),(.30,length,.28),'Graphite',.055)
        for y in [-length*.38,length*.38]:
            box('Corner stabiliser',(x,y,.34),(.52,.56,.24),'Alloy',.04)
            box('Visibility cap',(x,y,.47),(.38,.39,.045),'Orange',.012)
    for y in [-length*.34,length*.34]:box('Cross member',(0,y,.40),(width,.22,.20),'Alloy',.03)

def instrument_face(x,y,z):
    box('Recessed control face',(x,y,z),(1.05,.075,.55),'Graphite',.055)
    box('Status display',(x-.14,y-.045,z+.03),(.58,.028,.25),'Solar',.012)
    for i in range(3):box('Status segment',(x-.34+i*.19,y-.063,z+.03),(.10,.016,.075),'Signal',.007)
    cyl('Service connector',(x+.35,y-.055,z-.08),.065,.035,'Alloy',16,(0,1,0))

def return_capsule():
    skid(3.35,3.5)
    cyl('Heat shield perimeter',(0,0,.69),1.56,.28,'Graphite',48)
    ring('Ablative rim',(0,0,.82),1.50,.075,'Alloy')
    # Broad conical return-vehicle silhouette, with a closed body and docking collar.
    lathe('Ceramic reentry shell',[(0,.86),(1.43,.86),(1.47,1.02),(1.39,1.28),(.83,2.54),(.64,2.85),(.57,3.01),(0,3.01)])
    ring('Shoulder joint',(0,0,1.10),1.42,.032,'Graphite')
    cyl('Docking neck',(0,0,3.05),.58,.22,'Alloy',40)
    cyl('Docking protective cover',(0,0,3.19),.55,.10,'Graphite',40)
    ring('Docking lock',(0,0,3.22),.48,.04,'Orange')
    for i in range(12):
        a=i*math.tau/12
        beam('Heat shield tile seam',(1.1*math.cos(a),1.1*math.sin(a),.56),(1.52*math.cos(a),1.52*math.sin(a),.56),.009,'Alloy',6)
        cyl('Rim fastener',(1.45*math.cos(a),1.45*math.sin(a),.94),.025,.06,'Alloy',8)
    for i in range(6):
        a=i*math.tau/6
        beam('Panel seam',(1.416*math.cos(a),1.416*math.sin(a),1.2),(.76*math.cos(a),.76*math.sin(a),2.65),.011,'Graphite',6)
    # Flush inspection window on a sloping panel rather than a cartoon porthole.
    hatch=box('Access hatch',(0,-1.18,1.72),(.69,.10,.71),'Alloy',.08);hatch.rotation_euler.x=math.radians(-25)
    window=box('Blue recessed optic',(0,-1.248,1.80),(.47,.025,.33),'Solar',.045);window.rotation_euler.x=math.radians(-25)
    box('Rescue datum',(0,-1.39,1.26),(.54,.035,.085),'Orange',.015)
    for sign in [-1,1]:
        beam('Cradle strut',(sign*1.43,.90,.47),(sign*1.15,.75,1.52),.075,'Alloy',12)
        cyl('Gas service port',(sign*1.34,-.45,1.26),.11,.13,'Graphite',20,(sign,0,0))
    instrument_face(0,-1.75,.63)

def orbital_satellite():
    skid(2.9,2.7)
    cyl('Service bearing',(0,0,.70),.65,.46,'Alloy',32)
    box('Spacecraft bus',(0,0,1.65),(1.42,1.36,1.5),'Graphite',.10)
    for sign in [-1,1]:
        box('MLI thermal blanket',(0,sign*.705,1.65),(1.26,.075,1.30),'Foil',.065)
        for i in range(5):beam('Blanket stitched fold',(-.55,sign*.751,1.10+i*.24),(.55,sign*.751,1.17+i*.24),.009,'Alloy',6)
        box('Corner rail',(sign*.72,0,1.66),(.085,1.45,1.53),'Ceramic',.02)
        beam('Solar wing hinge',(sign*.70,0,1.78),(sign*3.12,0,1.78),.07,'Alloy',12)
        for j in range(2):
            x=sign*(1.58+j*1.26)
            box('Solar rigid panel',(x,0,1.82),(1.18,2.25,.11),'Alloy',.025)
            for ix in range(3):
                for iy in range(6):box('Photovoltaic cell',(x+(ix-1)*.352,(iy-2.5)*.351,1.885),(.325,.324,.024),'Solar',.009)
            for y in [-1.07,1.07]:box('Panel edge locator',(x,y,1.89),(.92,.035,.019),'Ceramic',.005)
    box('Avionics upper deck',(0,0,2.44),(1.49,1.43,.15),'Ceramic',.04)
    cyl('Antenna stem',(0,.14,2.68),.12,.39,'Alloy',20)
    lathe('High gain antenna reflector',[(0,2.86),(.18,2.88),(.36,2.96),(.61,3.12),(.67,3.17),(.65,3.21),(.59,3.16),(.35,3.02),(.17,2.95),(0,2.94)],'Ceramic',40)
    beam('Antenna feed',(0,0,2.95),(0,0,3.48),.035,'Graphite',12)
    cyl('Antenna feed cap',(0,0,3.48),.075,.12,'Orange',16)
    for x in [-.57,.57]:
        cyl('Reaction control nozzle',(x,-.79,2.21),.095,.23,'Alloy',16,(0,1,0))
    instrument_face(0,-1.41,.66)

def telemetry_dish():
    skid(3.3,3.2)
    cyl('Azimuth drive',(0,0,.69),.87,.49,'Graphite',40)
    ring('Azimuth scale',(0,0,.91),.83,.035,'Orange')
    for sign in [-1,1]:
        box('Fork arm',(sign*.73,0,1.57),(.23,.48,1.35),'Ceramic',.065)
        cyl('Elevation bearing',(sign*.88,0,2.16),.24,.24,'Alloy',24,(1,0,0))
    # Tilt the complete reflector/feed assembly so the dish reads from the game camera.
    start=len(CURRENT)
    profile=[(0,0),(.30,.02),(.65,.10),(1.05,.25),(1.43,.46),(1.63,.62),(1.61,.68),(1.40,.51),(1.02,.30),(.62,.15),(.28,.07),(0,.05)]
    lathe('Segmented tracking reflector',profile,'Ceramic',56)
    ring('Reflector perimeter',(0,0,.65),1.62,.038,'Alloy')
    for i in range(8):
        a=i*math.tau/8
        points=[(.22,.06),(.62,.15),(1.02,.30),(1.40,.51),(1.61,.69)]
        for (r,z),(rr,zz) in zip(points,points[1:]):beam('Reflector radial joint',(math.cos(a)*r,math.sin(a)*r,z),(math.cos(a)*rr,math.sin(a)*rr,zz),.012,'Graphite',6)
    for a in [0,math.tau/3,2*math.tau/3]:beam('Receiver strut',(1.43*math.cos(a),1.43*math.sin(a),.51),(0,0,1.49),.027,'Alloy',8)
    cyl('Feed receiver',(0,0,1.48),.12,.28,'Graphite',20)
    cyl('Receiver cap',(0,0,1.63),.13,.035,'Orange',20)
    rotation=Quaternion((1,0,0),math.radians(37))
    for o in CURRENT[start:]:o.location=rotation@o.location+Vector((0,0,2.15));o.rotation_euler=(rotation@o.rotation_euler.to_quaternion()).to_euler()
    box('Telemetry equipment',(0,.82,1.23),(1.25,.58,.67),'Ceramic',.07)
    instrument_face(0,-1.56,.66)

stats={}
for index,(name,build) in enumerate([('ReturnCapsule',return_capsule),('OrbitalSatellite',orbital_satellite),('TelemetryDish',telemetry_dish)]):
    CURRENT=[];build();objects=CURRENT[:]
    col=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(col)
    for o in objects:
        for old in list(o.users_collection):old.objects.unlink(o)
        col.objects.link(o)
    groups=[]
    batches=[(mat,[o for o in objects if o.data.materials[0]==mat]) for mat in MATS.values()]
    for mat,obs in batches:
        if not obs:continue
        bpy.ops.object.select_all(action='DESELECT')
        for o in obs:o.select_set(True)
        bpy.context.view_layer.objects.active=obs[0]
        if len(obs)>1:bpy.ops.object.join()
        o=bpy.context.object;o.name=name+'_'+mat.name
        bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');groups.append(o)
    stats[name]=[]
    for level,ratio in enumerate([1,.43,.17]):
        exports=[]
        for original in groups:
            o=original.copy();o.data=original.data.copy();bpy.context.collection.objects.link(o);exports.append(o)
            if ratio<1 and original.data.materials[0].name!='Route_Signal':
                bpy.context.view_layer.objects.active=o;mod=o.modifiers.new('Geometric LOD','DECIMATE');mod.ratio=ratio;bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.ops.object.select_all(action='DESELECT')
        for o in exports:o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,name+'_LOD'+str(level)+'.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True)
        tris=0
        for o in exports:o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles)
        stats[name].append(tris)
        for o in exports:bpy.data.objects.remove(o,do_unlink=True)
    for o in groups:o.location.x+=(index-1)*8
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.color_type='MATERIAL';area.spaces.active.region_3d.view_distance=24
            area.spaces.active.region_3d.view_location=(0,0,1.5)
            area.spaces.active.region_3d.view_rotation=Quaternion((.84,.49,.12,.20)).normalized()
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'AerospaceResearchDetails.blend'))
with open(os.path.join(SOURCE,'mesh-statistics.json'),'w') as f:json.dump(stats,f,indent=2)
print('RESEARCH_ACCENTS',stats)
