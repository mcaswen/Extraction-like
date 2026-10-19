"""Editable Blender hard-surface scenery for the forest recovery station.
Usage: blender -b --python tools/art/build_forest_recovery.py -- /path/to/UnityProject
Only original mesh art is generated. Exported scenery contains no physics/gameplay.
"""
import bpy, math, os, sys, json
from mathutils import Vector, Quaternion

ROOT=os.path.abspath(sys.argv[sys.argv.index('--')+1])
OUT=os.path.join(ROOT,'Assets/Art/Environment/ForestRecoveryStation/Models')
SOURCE=os.path.join(ROOT,'SourceArt/ForestRecoveryStation')
os.makedirs(OUT,exist_ok=True);os.makedirs(SOURCE,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
MATS={};CURRENT=[];PART='Body'
for key,color,metal,rough in [('Ceramic',(.70,.77,.79,1),.12,.34),('Graphite',(.035,.053,.065,1),.45,.42),
    ('Alloy',(.35,.46,.51,1),.78,.31),('Orange',(.90,.235,.045,1),.15,.40),
    ('Signal',(.09,.64,.78,1),.15,.3),('Solar',(.018,.065,.12,1),.45,.28),
    ('Nozzle',(.14,.17,.19,1),.8,.48),('Copper',(.36,.19,.095,1),.75,.42)]:
    m=bpy.data.materials.new('Forest_'+key);m.diffuse_color=color;m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=color;p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    if key=='Signal':p.inputs['Emission Color'].default_value=color;p.inputs['Emission Strength'].default_value=.7
    MATS[key]=m

def finish(o,name,mat,bevel=0):
    o.name=name;o['part']=PART;o.data.materials.append(MATS[mat]);CURRENT.append(o)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        m=o.modifiers.new('Manufactured edge radii','BEVEL');m.width=bevel;m.segments=2
        bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=m.name)
    for p in o.data.polygons:p.use_smooth=True
    m=o.modifiers.new('Weighted hard surface normals','WEIGHTED_NORMAL');m.keep_sharp=True
    bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=m.name)
    return o
def box(name,p,s,mat='Ceramic',bevel=.045):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.scale=s;return finish(o,name,mat,bevel)
def cyl(name,p,r,d,mat='Alloy',n=24,direction=(0,0,1)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=n,radius=r,depth=d,location=p);o=bpy.context.object;o.rotation_euler=Vector(direction).to_track_quat('Z','Y').to_euler();return finish(o,name,mat,min(.018,r*.07))
def beam(name,a,b,r=.055,mat='Alloy',n=10):
    a,b=Vector(a),Vector(b);return cyl(name,(a+b)*.5,r,(b-a).length,mat,n,b-a)
def tube(name,points,r=.055,mat='Alloy',n=8):
    for a,b in zip(points,points[1:]):beam(name,a,b,r,mat,n)
def ring(name,p,r,t,mat='Alloy',axis=(0,0,1)):
    bpy.ops.mesh.primitive_torus_add(major_radius=r,minor_radius=t,major_segments=48,minor_segments=6,location=p);o=bpy.context.object;o.rotation_euler=Vector(axis).to_track_quat('Z','Y').to_euler();return finish(o,name,mat)
def lathe(name,profile,mat='Nozzle',n=64):
    vs=[(r*math.cos(i*math.tau/n),r*math.sin(i*math.tau/n),z) for r,z in profile for i in range(n)]
    fs=[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(len(profile)-1) for i in range(n)]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vs,[],fs);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True);return finish(o,name,mat)
def deck(w,l):
    box('Skid chassis',(0,0,.28),(w,l,.40),'Graphite',.12)
    for ix in [-1,1]:
        box('Ceramic edge sill',(ix*(w/2-.17),0,.48),(.29,l-.35,.18),'Ceramic',.055)
        for iy in [-1,1]:
            box('Transport corner',(ix*(w/2-.32),iy*(l/2-.4),.47),(.65,.85,.30),'Alloy',.065)
            box('Safety corner',(ix*(w/2-.34),iy*(l/2-.4),.64),(.45,.55,.035),'Orange',.01)
            cyl('Leveling foot',(ix*(w/2-.4),iy*(l/2-.5),.11),.32,.22,'Graphite',16)
    for i in range(6):
        y=(i-2.5)*(l-.7)/6
        box('Removable floor panel',(0,y,.50),(w-.90,(l-.9)/6-.07,.09),'Alloy',.025)
        for x in [-w*.32,w*.32]:box('Drainage grate',(x,y,.553),(.60,(l-.9)/6-.24,.016),'Graphite',.01)
    for y in [-l/2+.22,l/2-.22]:box('Threshold',(0,y,.52),(w-.75,.18,.10),'Ceramic',.025)
def console(p=(0,0,0)):
    x,y,z=p
    box('Field controller',(x,y,z+.72),(1.5,.88,1.4),'Ceramic',.11)
    box('Recessed equipment panel',(x,y-.455,z+.87),(1.22,.035,.73),'Graphite',.04)
    box('Instrument glass',(x-.16,y-.48,z+.96),(.65,.025,.34),'Solar',.025)
    for i in range(3):box('Acquisition indicator',(x-.38+i*.22,y-.5,z+.94),(.12,.018,.09),'Signal',.006)
    for sign in [-1,1]:
        cyl('Protected cable coupling',(x+sign*.48,y-.49,z+.55),.09,.10,'Alloy',16,(0,1,0))
    box('Lift handle',(x,y,z+1.48),(.7,.25,.08),'Graphite',.02)
    for yoff in [-.24,0,.24]:box('Cooling louvre',(x+.76,y+yoff,z+.82),(.025,.12,.55),'Graphite',.005)
def rocket_badge(p,size=1.0):
    x,y,z=p;v=[(-.1,-.25),(.1,-.25),(.1,.2),(0,.43),(-.1,.2),(-.1,-.03),(-.26,-.26),(-.26,-.42),(-.1,-.25),(.1,-.03),(.26,-.26),(.26,-.42),(.1,-.25)]
    me=bpy.data.meshes.new('Recovery emblem');me.from_pydata([(x+a*size,y+b*size,z) for a,b in v],[],[(0,1,2,3,4),(5,6,7,8),(9,10,11,12)]);me.update();o=bpy.data.objects.new('Recovery emblem',me);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;finish(o,o.name,'Ceramic')

def engine_station():
    global PART
    deck(8.8,9.6)
    # Visible load-bearing cradle, open sides and asymmetric service equipment.
    for x in [-1.45,1.45]:
        box('Cradle base rail',(x,.35,.77),(.38,5.4,.35),'Graphite',.055)
        for y in [-1.3,1.7]:
            beam('Angled engine support',(x*1.23,y,.85),(x,y,2.77),.13,'Alloy',12)
            cyl('Trunnion pin',(x,y,2.78),.24,.42,'Orange',24,(1,0,0))
    # Real open bell with internal wall and throat, canted towards the game camera.
    start=len(CURRENT)
    profile=[(.64,-.5),(.72,-.35),(.72,.10),(.61,.40),(.47,.72),(.45,1.00),(.54,1.24),(.70,1.52),(.94,1.89),(1.22,2.25),(1.51,2.61),(1.78,2.96),(1.87,3.15),(1.87,3.24),(1.76,3.24),(1.67,3.02),(1.41,2.69),(1.12,2.33),(.84,1.97),(.60,1.60),(.44,1.31),(.35,1.02),(.36,.78),(.49,.40),(.59,.10),(.59,-.35),(.64,-.5)]
    lathe('Recovered regenerative engine bell',profile)
    ring('Nozzle exit reinforcement',(0,0,3.20),1.83,.058,'Alloy')
    ring('Heat affected copper rim',(0,0,2.99),1.79,.035,'Copper')
    for i in range(32):
        a=i*math.tau/32;pts=[(r*math.cos(a),r*math.sin(a),z) for r,z in [(1.79,3.04),(1.55,2.68),(1.27,2.31),(.99,1.95),(.75,1.58),(.59,1.28),(.50,1.02)]]
        tube('Cooling channel rib',pts,.018,'Copper',6)
    for z,r in [(-.32,.73),(.15,.70),(.48,.61)]:ring('Chamber clamp',(0,0,z),r,.075,'Alloy')
    cyl('Injector backplate',(0,0,-.54),.76,.16,'Alloy',48)
    for i in range(12):
        a=i*math.tau/12;cyl('Injector flange bolt',(.66*math.cos(a),.66*math.sin(a),-.65),.045,.08,'Graphite',8)
    for sign in [-1,1]:
        tube('External propellant manifold',[(sign*.5,.25,-.63),(sign*.98,.25,-.63),(sign*1.12,.25,.15),(sign*.86,.25,.53)],.10,'Alloy',12)
        cyl('Turbopump housing',(sign*1.12,.28,-.12),.30,.56,'Graphite',24,(0,1,0))
        ring('Pump band',(sign*1.12,0,-.12),.28,.035,'Orange',(0,1,0))
    rotation=Quaternion((1,0,0),math.radians(55));origin=Vector((0,.9,3.10))
    for o in CURRENT[start:]:o.location=rotation@o.location+origin;o.rotation_euler=(rotation@o.rotation_euler.to_quaternion()).to_euler()
    for sign in [-1,1]:
        x=sign*3.1
        box('Inspection column base',(x,.8,.76),(.9,1.6,.45),'Graphite',.08)
        box('Inspection upright',(x,1.10,2.47),(.40,.52,3.45),'Ceramic',.075)
        box('Linear bearing track',(x-.21*sign,1.10,2.63),(.07,.29,2.7),'Alloy',.02)
        cyl('Arm articulation',(x,.85,3.80),.30,.40,'Graphite',24,(0,1,0))
        beam('Inspection cantilever',(x,1.1,3.8),(x-sign*.90,-.45,4.6),.12,'Ceramic',12)
        beam('Tension member',(x,1.1,2.65),(x-sign*.9,-.45,4.6),.065,'Alloy',10)
    PART='Scanner'
    box('Moving optical head',(-2.20,-.48,4.6),(.65,.72,.40),'Graphite',.075)
    for x in [-2.37,-2.04]:cyl('Detector optic',(x,-.85,4.58),.10,.06,'Signal',20,(0,1,0))
    box('Sensor shroud',(-2.20,-.45,4.83),(.77,.83,.06),'Ceramic',.025)
    PART='Body'
    box('Fixed inspection camera',(2.20,-.48,4.60),(.62,.69,.38),'Ceramic',.065)
    cyl('Fixed aperture',(2.20,-.85,4.6),.16,.06,'Solar',20,(0,1,0))
    console((-3.18,-3.25,.57))
    tube('Controller harness',[(-3.1,-2.8,.65),(-3.1,-1.6,.64),(-2.65,-1.2,.65),(-2.65,1.15,.65),(-2.7,1.15,2.40)],.075,'Graphite')
    box('Sample drawer',(3.12,-2.6,.96),(1.35,2.4,.8),'Ceramic',.12)
    box('Top sample tray',(3.12,-2.6,1.4),(1.10,2.10,.08),'Graphite',.05)
    for y in [-3.15,-2.60,-2.05]:cyl('Transport sample canister',(3.12,y,1.58),.22,.30,'Alloy',20)
    rocket_badge((0,-3.75,.565),1.65)
    for x in [-3.95,3.95]:
        for y in [-3.7,3.7]:box('Service locator light',(x,y,.65),(.18,.56,.07),'Signal',.02)
    for x in [-3.75,3.75]:
        box('Lamp mast',(x,3.85,2.44),(.12,.16,3.6),'Alloy',.02)
        box('Worklight hood',(x,3.72,4.32),(.85,.48,.20),'Graphite',.055)
        box('Worklight diffuser',(x,3.68,4.20),(.65,.32,.035),'Ceramic',.015)

def power_unit():
    deck(4.6,4.4)
    console((0,-1.15,.58))
    box('Battery equipment',(0,.40,1.42),(1.75,1.50,1.75),'Graphite',.12)
    for x in [-.72,.72]:box('Battery armor',(x,.4,1.47),(.22,1.55,1.75),'Ceramic',.06)
    for i in range(5):box('Cooling ribs',(0,1.18,.91+i*.23),(1.25,.09,.10),'Alloy',.01)
    for sign in [-1,1]:
        x=sign*1.30
        beam('Panel support',(sign*.60,.8,.68),(x,.8,2.15),.085,'Alloy')
        box('Solar frame',(x,.60,2.28),(1.8,2.7,.12),'Alloy',.025)
        for ix in range(3):
            for iy in range(5):box('Photovoltaic module',(x+(ix-1)*.54,.60+(iy-2)*.5,2.355),(.49,.46,.025),'Solar',.008)
    for sign in [-1,1]:cyl('Cable reel',(sign*1.55,-1.05,1.03),.40,.44,'Graphite',24,(1,0,0))

def entry_marker():
    deck(2.8,2.1)
    box('Marker plinth',(0,0,1.22),(2.3,.75,1.35),'Graphite',.15)
    for x in [-1.05,1.05]:box('Marker shoulder',(x,0,1.45),(.22,.88,1.75),'Ceramic',.055)
    box('Tilted enamel face',(0,-.03,1.97),(1.85,1.2,.12),'Solar',.07)
    rocket_badge((-.48,-.03,2.04),.9)
    # Geometric identification bars remain readable even when text is tiny.
    for i in range(3):box('Station number bars',(.30+i*.22,-.04,2.04),(.12,.60,.025),'Ceramic',.007)
    box('Amber location stripe',(0,-.405,1.37),(1.62,.035,.10),'Orange',.015)
    box('Status aperture',(0,-.43,1.63),(.35,.025,.055),'Signal',.01)
    cyl('Antenna rod',(.87,.24,2.6),.035,1.25,'Alloy',12)

def tracking_terminal():
    global PART
    deck(3.8,3.8)
    cyl('Fixed azimuth pedestal',(0,0,.90),.9,.65,'Graphite',40)
    ring('Bearing race',(0,0,1.18),.83,.055,'Orange')
    console((0,-1.25,.55))
    PART='Azimuth'
    for sign in [-1,1]:
        box('Gimbal fork',(sign*.82,0,1.88),(.24,.48,1.55),'Ceramic',.075)
        cyl('Elevation bearing',(sign*.92,0,2.43),.25,.25,'Alloy',24,(1,0,0))
    start=len(CURRENT)
    lathe('Segmented telemetry reflector',[(0,0),(.30,.02),(.65,.10),(1.05,.25),(1.43,.46),(1.63,.62),(1.61,.69),(1.4,.53),(1.02,.32),(.62,.17),(.28,.09),(0,.07)],'Ceramic',56)
    ring('Dish lip',(0,0,.65),1.62,.035,'Alloy')
    for i in range(8):
        a=i*math.tau/8
        tube('Reflector panel seam',[(r*math.cos(a),r*math.sin(a),z) for r,z in [(.26,.09),(.62,.17),(1.02,.32),(1.4,.53),(1.60,.70)]],.012,'Graphite',6)
    for i in range(3):
        a=i*math.tau/3;beam('Receiver support',(1.42*math.cos(a),1.42*math.sin(a),.53),(0,0,1.5),.035,'Alloy')
    cyl('Telemetry receiver',(0,0,1.5),.13,.26,'Graphite',20)
    cyl('Receiver ceramic cap',(0,0,1.65),.14,.04,'Orange',20)
    rot=Quaternion((1,0,0),math.radians(32))
    for o in CURRENT[start:]:o.location=rot@o.location+Vector((0,0,2.44));o.rotation_euler=(rot@o.rotation_euler.to_quaternion()).to_euler()
    PART='Body'

stats={}
for index,(name,build) in enumerate([('EngineInspectionRig',engine_station),('FieldPowerModule',power_unit),('RecoveryMarker',entry_marker),('TrackingTerminal',tracking_terminal)]):
    CURRENT=[];PART='Body';build();objects=CURRENT[:]
    collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(collection)
    for o in objects:
        for old in list(o.users_collection):old.objects.unlink(o)
        collection.objects.link(o)
    groups=[]
    batches=[(part,key,[o for o in objects if o.get('part')==part and o.data.materials[0]==mat]) for part in ['Body','Scanner','Azimuth'] for key,mat in MATS.items()]
    for part,key,obs in batches:
            if not obs:continue
            bpy.ops.object.select_all(action='DESELECT')
            for o in obs:o.select_set(True)
            bpy.context.view_layer.objects.active=obs[0]
            if len(obs)>1:bpy.ops.object.join()
            o=bpy.context.object;o.name=name+'_'+part+'_Forest_'+key
            bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');groups.append(o)
    stats[name]=[]
    for level,ratio in enumerate([1,.42,.16]):
        exports=[]
        for orig in groups:
            o=orig.copy();o.data=orig.data.copy();collection.objects.link(o);exports.append(o)
            if ratio<1 and orig.data.materials[0].name not in ['Forest_Signal','Forest_Solar']:
                bpy.context.view_layer.objects.active=o;mod=o.modifiers.new('Gameplay LOD','DECIMATE');mod.ratio=ratio;bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.ops.object.select_all(action='DESELECT')
        for o in exports:o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,name+'_LOD'+str(level)+'.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True)
        tris=0
        for o in exports:o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles)
        stats[name].append(tris)
        for o in exports:bpy.data.objects.remove(o,do_unlink=True)
    for o in groups:o.location.x+=index*13
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.color_type='MATERIAL';area.spaces.active.region_3d.view_distance=24;area.spaces.active.region_3d.view_location=(7,0,2)
            area.spaces.active.region_3d.view_rotation=Quaternion((.84,.49,.12,.20)).normalized()
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'ForestRecoveryStation.blend'))
with open(os.path.join(SOURCE,'mesh-statistics.json'),'w') as f:json.dump(stats,f,indent=2)
print('FOREST_RECOVERY_MODELS',stats)
