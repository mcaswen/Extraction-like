"""Derive a game-ready emergency-landing vehicle without changing the intact master.

Run with Blender 4.2 --background --python this_file -- --source master.blend --out folder.
The three FBXs use the same origin; +Z in Blender becomes +Y in Unity.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path
from collections import defaultdict
import bpy
import bmesh
from mathutils import Vector, Matrix

args = argparse.ArgumentParser()
args.add_argument('--source', required=True)
args.add_argument('--out', required=True)
opt = args.parse_args(sys.argv[sys.argv.index('--')+1:])
src, out = Path(opt.source).resolve(), Path(opt.out).resolve()
assert src.parent != out, 'Preserve the intact source directory.'
source_hash = hashlib.sha256(src.read_bytes()).hexdigest()
out.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(src))
bpy.context.preferences.filepaths.save_version = 0
source = bpy.data.collections['ROCKET • 完整装配']
originals = list(source.all_objects)
for col in bpy.data.collections: col.hide_viewport = False
bpy.context.view_layer.update()

PALETTE = {
    'Hull': ((.73,.74,.695), .025, .69),
    'Trim': ((.30,.365,.385), .23, .60),
    'Orange': ((.64,.185,.043), .015, .74),
    'Steel': ((.40,.46,.48), .80, .29),
    'Dark': ((.048,.065,.075), .20, .72),
    'WarmMetal': ((.31,.245,.16), .62, .49),
    'Ink': ((.018,.036,.046), .02, .68),
    'FlagRed': ((.75,.008,.018), .0, .61),
    'FlagGold': ((1,.70,.035), .0, .55),
    'Scorch': ((.065,.060,.052), .03, .88),
}
materials={}
for key,(base,metal,rough) in PALETTE.items():
    m=bpy.data.materials.new('WC01_'+key); m.use_nodes=True; m.diffuse_color=(*base,1)
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'); bs.inputs['Base Color'].default_value=(*base,1)
    bs.inputs['Metallic'].default_value=metal; bs.inputs['Roughness'].default_value=rough
    attr=m.node_tree.nodes.new('ShaderNodeVertexColor'); attr.layer_name='LandingPaint'
    m.node_tree.links.new(attr.outputs['Color'],bs.inputs['Base Color'])
    bs.inputs['Emission Color'].default_value=(1,.30,.035,1)
    m.node_tree.links.new(attr.outputs['Alpha'],bs.inputs['Emission Strength'])
    materials[key]=m

def category(ob):
    name=ob.data.materials[0].name if ob.data.materials else ''
    if 'FlagRed' in name: return 'FlagRed'
    if 'FlagGold' in name: return 'FlagGold'
    if 'Ink' in name: return 'Ink'
    if 'Orange' in name or 'Amber' in name: return 'Orange'
    if any(k in name for k in ['Graphite','Dark','Composite','Teal']): return 'Dark'
    if any(k in name for k in ['Copper','Nickel','Insulation']): return 'WarmMetal'
    if any(k in name for k in ['Steel','Titanium']): return 'Steel'
    if 'Alternate' in name: return 'Trim'
    if 'R01_Equipment' in name: return 'Steel'
    return 'Hull'

collection=bpy.data.collections.new('WC01_LANDING_LOD0'); bpy.context.scene.collection.children.link(collection)
parts=[]; omitted=[]
def add_mesh(name,verts,faces,key,smooth=False):
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    ob=bpy.data.objects.new(name,mesh); collection.objects.link(ob); mesh.materials.append(materials[key])
    ob['surface_class']=key
    for p in mesh.polygons:p.use_smooth=smooth
    parts.append(ob);return ob

def apply_modifier(ob,modifier):
    bpy.context.view_layer.objects.active=ob
    bpy.ops.object.modifier_apply(modifier=modifier.name)

def simplify(ob,ratio):
    if ratio>=.999 or len(ob.data.polygons)<50:return
    mod=ob.modifiers.new('Game silhouette reduction','DECIMATE');mod.ratio=ratio
    mod.use_collapse_triangulate=True;apply_modifier(ob,mod)

for ob in originals:
    if ob.type!='MESH':continue
    size=max(ob.dimensions)
    text=ob.get('stencil_text','')
    flag='中国国旗' in ob.name
    if (size<.17 and not flag) or (text and text not in ['文昌一号','WC–01','中国航天']):
        omitted.append(ob.name);continue
    if 'R01 engine' in ob.name and any(k in ob.name for k in ['ChannelLands','Fasteners','Markings','Identification','EquipmentCooling','Fittings','SupportBrackets','JacketWindow']):
        omitted.append(ob.name);continue
    for mod in ob.modifiers:
        if mod.type=='SUBSURF':mod.show_viewport=False
        elif mod.type=='BEVEL':mod.segments=1
    bpy.context.view_layer.update()
    deps=bpy.context.evaluated_depsgraph_get()
    mesh=bpy.data.meshes.new_from_object(ob.evaluated_get(deps),depsgraph=deps)
    mesh.transform(ob.matrix_world)
    cp=bpy.data.objects.new(ob.name,mesh);collection.objects.link(cp);cp['surface_class']=category(ob)
    mesh.materials.clear();mesh.materials.append(materials[cp['surface_class']])
    for p in mesh.polygons:p.material_index=0
    # Keep the large Chinese identification as geometry; remove redundant tessellation.
    if flag or text:
        bm=bmesh.new();bm.from_mesh(mesh)
        bmesh.ops.dissolve_limit(bm,angle_limit=.018,verts=list(bm.verts),edges=list(bm.edges),use_dissolve_boundaries=False)
        bm.to_mesh(mesh);bm.free();mesh.update()
        cp['identity_artwork']=True
    elif 'R01 engine' in ob.name:simplify(cp,.09)
    elif len(mesh.polygons)>10000:simplify(cp,.07)
    elif len(mesh.polygons)>650:simplify(cp,.28)
    # One broad inward dent on the lower core, away from the vehicle name and flag.
    if 'Main stage • continuous' in ob.name:
        bm=bmesh.new();bm.from_mesh(mesh)
        edges=[e for e in bm.edges if abs(e.verts[0].co.z-e.verts[1].co.z)>1.4]
        if edges:bmesh.ops.subdivide_edges(bm,edges=edges,cuts=10,use_grid_fill=True)
        bm.to_mesh(mesh);bm.free()
        for v in mesh.vertices:
            a=math.atan2(v.co.y,v.co.x);da=math.atan2(math.sin(a+.62),math.cos(a+.62))
            w=math.exp(-((v.co.z-6.1)/1.4)**2-(da/.31)**2)
            v.co.x*=1-.10*w;v.co.y*=1-.10*w
    # Broader, readable service spines; keep the central lettering area untouched.
    if any(k in ob.name for k in ['Protected equipment spine', 'Spine sealed', 'Spine captive', 'Spine titanium']):
        center=sum((v.co for v in mesh.vertices),Vector())/len(mesh.vertices)
        a=math.atan2(center.y,center.x);t=Vector((-math.sin(a),math.cos(a),0))
        for v in mesh.vertices:v.co+=t*((v.co-center).dot(t)*.32)
    # Bend only the lower outer corner of the front-right fin and its attached trim.
    if ob.get('assembly','').startswith('05'):
        for v in mesh.vertices:
            if v.co.x>.8 and v.co.y<-.8 and v.co.z<2.8:
                w=max(0,min(1,(Vector((v.co.x,v.co.y)).length-1.8)/1.4))*max(0,(2.8-v.co.z)/2.8)
                v.co.z+=.28*w;v.co.x+=.24*w
    for v in mesh.vertices:v.co.z+=1.18
    mesh.update();parts.append(cp)

def box(name,center,size,key,angle=0,bevel=.035):
    x,y,z=[v/2 for v in size]
    verts=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
    rot=Matrix.Rotation(angle,4,'Z');vs=[Vector(center)+rot@Vector(v) for v in verts]
    ob=add_mesh(name,vs,[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],key)
    if bevel:
        m=ob.modifiers.new('Visible game edge','BEVEL');m.width=bevel;m.segments=1;apply_modifier(ob,m)
        m=ob.modifiers.new('Hard surface normals','WEIGHTED_NORMAL');apply_modifier(ob,m)
    return ob

def rod(name,a,b,r,key,sides=12):
    a,b=Vector(a),Vector(b);d=b-a;q=d.to_track_quat('Z','Y')
    verts=[a+q@Vector((r*math.cos(i*math.tau/sides),r*math.sin(i*math.tau/sides),z)) for z in [0,d.length] for i in range(sides)]
    faces=[(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)]
    faces.extend([tuple(reversed(range(sides))),tuple(range(sides,2*sides))])
    return add_mesh(name,verts,faces,key,True)

def beam(name,a,b,width,depth,key,side):
    a,b=Vector(a),Vector(b);direction=(b-a).normalized();t=Vector(side).normalized();n=direction.cross(t).normalized()
    vs=[p+t*x*width/2+n*y*depth/2 for p in [a,b] for x,y in [(-1,-1),(1,-1),(1,1),(-1,1)]]
    ob=add_mesh(name,vs,[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],key)
    m=ob.modifiers.new('Structural edge radius','BEVEL');m.width=.035;m.segments=2;apply_modifier(ob,m)
    m=ob.modifiers.new('Structural weighted normals','WEIGHTED_NORMAL');apply_modifier(ob,m)
    return ob

def shell_patch(name,angle,width,z0,z1,radius,key,steps=12):
    vs=[]
    for z in [z0,z1]:
        for j in range(steps+1):
            a=angle+width*(j/steps-.5);vs.append((radius*math.cos(a),radius*math.sin(a),z))
    return add_mesh(name,vs,[(j,j+1,j+steps+2,j+steps+1) for j in range(steps)],key,True)

# Four deliberate, readable landing legs. The nozzle stays clear of the ground.
feet=[]
for i in range(4):
    a=math.pi/4+i*math.pi/2;u=Vector((math.cos(a),math.sin(a),0));t=Vector((-u.y,u.x,0))
    foot=u*4.65;foot.z=.18;feet.append(tuple(foot))
    upper=u*1.99+t*.48;upper.z=5.55
    knee=u*3.87+t*.34;knee.z=1.55-(.16 if i==3 else 0)
    ankle=foot+Vector((0,0,.43))
    box('Landing leg %d • hull load transfer saddle'%i,upper-u*.06,(.48,1.0,.94),'Dark',a,.08)
    rod('Landing leg %d • transverse upper trunnion'%i,upper-t*.53,upper+t*.53,.30,'Steel',24)
    for sign in [-1,1]:
        beam('Landing leg %d • forged fork cheek'%i,upper+t*.28*sign,knee+t*.25*sign,.18,.35,'Trim',t)
    beam('Landing leg %d • closed central load web'%i,upper-Vector((0,0,.22)),knee+Vector((0,0,.12)),.34,.22,'Dark',t)
    beam('Landing leg %d • outer wear plate'%i,upper+u*.20-Vector((0,0,.40)),knee+u*.20+Vector((0,0,.35)),.36,.08,'Hull',t)
    beam('Landing leg %d • restrained identification stripe'%i,upper+u*.245-Vector((0,0,.55)),upper.lerp(knee,.48)+u*.245,.13,.028,'Orange',t)
    piston_start=u*2.0-t*.20+Vector((0,0,4.15))
    piston_end=ankle-t*.12
    barrel_end=piston_start.lerp(piston_end,.68 if i==3 else .61)
    rod('Landing leg %d • hydraulic barrel'%i,piston_start,barrel_end,.205,'Dark',20)
    rod('Landing leg %d • chrome damper'%i,barrel_end,piston_end,.125,'Steel',20)
    axis=(piston_end-piston_start).normalized()
    rod('Landing leg %d • damper seal collar'%i,barrel_end-axis*.10,barrel_end+axis*.08,.25,'Steel',24)
    rod('Landing leg %d • knee bearing'%i,knee-t*.39,knee+t*.39,.28,'Dark',24)
    beam('Landing leg %d • lower load link'%i,knee,ankle,.34,.25,'Trim',t)
    rod('Landing leg %d • spherical foot pivot'%i,ankle-t*.31,ankle+t*.31,.22,'Steel',20)
    for sign in [-1,1]:
        rod('Landing leg %d • hinge axle cap'%i,upper+t*.54*sign,upper+t*.59*sign,.20,'Orange',16)
        rod('Landing leg %d • stabilising brace'%i,u*1.85+t*.48*sign+Vector((0,0,3.1)),knee+t*.25*sign,.075,'Steel',12)
    # Same contact plane and identical XY footprint as the previous version.
    box('Landing foot %d • load-spreading pad'%i,foot+Vector((0,0,.07)),(1.65,1.18,.42),'Dark',a,.065)
    box('Landing foot %d • upper wear plate'%i,foot+Vector((0,0,.30)),(1.38,.95,.06),'Trim',a,.025)
    for sign in [-1,1]:
        box('Landing foot %d • orange hazard edge'%i,foot+t*.43*sign+Vector((0,0,.345)),(1.18,.10,.035),'Orange',a,.006)
        box('Landing foot %d • bearing gusset'%i,foot+t*.24*sign+Vector((0,0,.42)),(.60,.12,.24),'Steel',a,.025)

# Medium-scale panel rhythm on the upper shoulder and side flanks, readable at gameplay distance.
for a in [math.pi/4+i*math.pi/2 for i in range(4)]:
    shell_patch('Upper stage • graphite equipment socket',a,.32,16.20,17.83,1.948,'Dark')
    shell_patch('Upper stage • raised satin equipment cover',a,.245,16.32,17.71,1.984,'Trim')
    u=Vector((math.cos(a),math.sin(a),0));t=Vector((-u.y,u.x,0))
    for z in [16.48,17.48]:box('Upper stage • keyed latch',u*2.01+Vector((0,0,z)),(.075,.38,.10),'Steel',a,.015)
    box('Upper stage • service register',u*2.04+Vector((0,0,16.8)),(.035,.14,.32),'Orange',a,.008)
for a in [0,math.pi]:
    shell_patch('Main stage • side equipment collar',a,.43,10.10,13.60,2.066,'Trim',18)
    shell_patch('Main stage • layered side fairing',a,.34,10.26,13.44,2.098,'Hull',18)
    for z in [10.65,12.95]:
        shell_patch('Main stage • fairing fastening belt',a,.40,z,z+.10,2.119,'Steel',16)
for a in [-math.pi/2, math.pi/2]:
    # A restrained seam following the original fairing profile, not extra floating armor.
    points=[]
    for j in range(14):
        z=20.55+j*(4.45/13);radius=1.925*(25.63-z)/5.33+.012
        points.append(Vector((radius*math.cos(a),radius*math.sin(a),z)))
    for j in range(len(points)-1):rod('Fairing • longitudinal split seam',points[j],points[j+1],.018,'Trim',6)
    for z in [21.0,22.1,23.2]:
        radius=1.925*(25.63-z)/5.33+.025
        box('Fairing • recessed separation latch',(radius*math.cos(a),radius*math.sin(a),z),(.055,.19,.075),'Steel',a,.01)
for sign in [-1,1]:
    for z in [4.3,8.25]:
        box('Booster • reinforced connecting bridge',(sign*2.25,0,z),(.72,.76,.43),'Dark',0,.07)
        box('Booster • bridge thermal cap',(sign*2.25,0,z+.25),(.68,.74,.09),'Trim',0,.04)
        rod('Booster • visible joint pin',(sign*2.31,-.43,z),(sign*2.31,.43,z),.16,'Steel',20)
for i in range(12):
    a=i*math.tau/12
    shell_patch('Propulsion • graphite thermal petal',a,.43,3.95,5.24,2.065,'Dark',10)
    shell_patch('Propulsion • petal edge register',a+.215,.022,4.02,5.17,2.08,'Trim',2)

# A torn outer service shield: shallow, not a hole through the propellant tank.
a=-.65;r=2.10;u=Vector((math.cos(a),math.sin(a),0));t=Vector((-u.y,u.x,0));c=u*r+Vector((0,0,6.25))
vs=[c+t*x+Vector((0,0,z))+u*lift for x,z,lift in [(-.66,-.83,.08),(.66,-.83,.08),(.66,-.08,.09),(.35,.18,.30),(-.18,.32,.42),(-.66,.10,.10),(-.28,.63,.58),(-.66,.51,.24)]]
panel=add_mesh('Damaged service shield • folded attached lower cover',vs,[(0,1,2,3,4,5),(5,4,6,7)],'Trim')
sol=panel.modifiers.new('Shield thickness','SOLIDIFY');sol.thickness=.045;apply_modifier(panel,sol)
back=[c+t*x+Vector((0,0,z))+u*.015 for x,z in [(-.65,-.80),(.65,-.80),(.65,.93),(-.65,.93)]]
add_mesh('Recessed access bay • dark backing',back,[(0,1,2,3)],'Dark')
for side in [-1,1]:
    box('Access bay • structural jamb',c+t*.70*side+Vector((0,0,.06))+u*.035,(.13,.10,1.94),'Steel',a,.02)
for z in [-.88,.99]:box('Access bay • structural sill',c+Vector((0,0,z))+u*.035,(.13,1.5,.10),'Steel',a,.02)
for k in range(3):
    start=c+t*(-.40+k*.32)+u*.09+Vector((0,0,-.45));mid=start+Vector((0,0,1.12))
    rod('Access bay • protected conduit',start,mid,.055,'WarmMetal',12)
    rod('Access bay • elbow outlet',mid,mid+t*.16,.055,'Steel',12)
    for z in [.10,.58]:box('Access bay • conduit clamp',c+t*(-.40+k*.32)+u*.15+Vector((0,0,z)),(.06,.15,.10),'Trim',a,.015)
valve=c+t*.31+u*.13+Vector((0,0,.42))
rod('Access bay • isolation valve',valve,valve+u*.13,.17,'Steel',20)
box('Access bay • safety lever',valve+u*.17,(.08,.30,.085),'Orange',a,.012)
indicator=box('Access bay • single amber status lens',c+t*.52+Vector((0,0,.78))+u*.13,(.06,.12,.25),'Orange',a,.02)
indicator['status_emission']=True
for k in range(4):
    angle=a+.24+k*.045
    scuff=shell_patch('Localized paint scrape',angle,.02,5.7-k*.12,6.3-k*.11,2.055,'Trim',2)
    scuff['paint_override']=(.24,.25,.24)

def paint(ob):
    key=ob['surface_class'];base=Vector(ob.get('paint_override',PALETTE[key][0]));me=ob.data
    for attr in list(me.color_attributes):me.color_attributes.remove(attr)
    colors=me.color_attributes.new(name='LandingPaint',type='FLOAT_COLOR',domain='POINT')
    for v,item in zip(me.vertices,colors.data):
        p=v.co;z=p.z-1.18;a=math.atan2(p.y,p.x)
        noise=.5+.25*math.sin(p.x*3.2+p.z*1.6)+.25*math.sin(p.y*4.5-p.z*2.7)
        soot=max(0,1-z/3.5)*(.30+.11*noise)
        da=math.atan2(math.sin(a+.65),math.cos(a+.65))
        streak=math.exp(-(da/.29)**2-((z-5.7)/1.25)**2)*.66
        runoff=math.exp(-((da-.09*math.sin(z*1.8))/.12)**2-((z-4.6)/1.7)**2)*.26
        dirt=min(.72,max(0,soot+streak+runoff)) if key in ['Hull','Trim','Orange','Steel','Dark'] else 0
        if ob.get('identity_artwork'):dirt=0
        c=base.lerp(Vector((.060,.057,.048)),dirt)
        # Broad edge highlights survive the normal gameplay camera; no random speckle noise.
        if key in ['Hull','Trim','Orange'] and not ob.get('identity_artwork'):
            c*=.99+.010*math.sin(z*1.7)
        # Alpha is a deliberately sparse emission mask, not transparency.
        item.color=(*c,1 if ob.get('status_emission') else 0)
    me.color_attributes.active_color=colors

for ob in parts:paint(ob)
# Hide source and remove its scene links, but keep the intact file untouched on disk.
for ob in originals:
    if ob.name in bpy.data.objects:bpy.data.objects.remove(ob,do_unlink=True)
for ob in list(bpy.data.objects):
    if ob not in parts:bpy.data.objects.remove(ob,do_unlink=True)

groups=defaultdict(list)
for ob in parts:groups[(ob['surface_class'],bool(ob.get('identity_artwork')))].append(ob)
combined=[]
for (key,identity),objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join()
    ob=objects[0];ob.name='WC01_'+key+('_Identity' if identity else '_Structure')
    ob['surface_class']=key;ob['identity_artwork']=identity
    # Join leaves duplicate slots; all faces in this batch deliberately share one material.
    ob.data.materials.clear();ob.data.materials.append(materials[key])
    for p in ob.data.polygons:p.material_index=0
    combined.append(ob)

def triangles(objects):
    total=0
    for ob in objects:ob.data.calc_loop_triangles();total+=len(ob.data.loop_triangles)
    return total

initial=triangles(combined)
if initial>145000:
    ratio=130000/initial
    for ob in combined:
        if not ob.get('identity_artwork'):simplify(ob,ratio)
lods=[combined]
for index,ratio in [(1,.43),(2,.14)]:
    col=bpy.data.collections.new('WC01_LANDING_LOD'+str(index));bpy.context.scene.collection.children.link(col)
    level=[]
    for ob in combined:
        cp=ob.copy();cp.data=ob.data.copy();col.objects.link(cp);cp.name=ob.name+'_LOD'+str(index)
        if not cp.get('identity_artwork'):simplify(cp,ratio)
        level.append(cp)
    lods.append(level)

# Decimation can extrapolate a foot corner. Never expand the LOD0 occupied envelope.
envelope=[(min(v.co[axis] for ob in combined for v in ob.data.vertices),
           max(v.co[axis] for ob in combined for v in ob.data.vertices)) for axis in range(3)]
for level in lods[1:]:
    for ob in level:
        for v in ob.data.vertices:
            for axis,(lo,hi) in enumerate(envelope):v.co[axis]=max(lo,min(hi,v.co[axis]))
        ob.data.update()

report={'revision':'game-camera structural and material polish','source_sha256':source_hash,'intact_master_unchanged':True,'feet_blender':feet,
        'smoke_anchors_blender':[[1.70,-1.30,6.6],[-3.18,-.69,3.0]],
        'lods':[],'material_settings':{k:{'metallic':v[1],'roughness':v[2]} for k,v in PALETTE.items()},
        'removed_subpixel_objects':len(omitted),
        'polish_features':['forged landing forks and hydraulic dampers','same foot contact plane and footprint','larger layered service fairings','graphite propulsion petals','attached folded shield and exposed service bay','single amber status lens','localized soot and runoff'],
        'emissive_vertices_lod0':sum(sum(c.color[3]>.5 for c in ob.data.color_attributes['LandingPaint'].data) for ob in combined)}
report['envelope_blender']=envelope
anchors=[]
for kind,positions in [('Foot',feet),('Smoke',report['smoke_anchors_blender'])]:
    for i,position in enumerate(positions):
        anchor=bpy.data.objects.new('Anchor_%s_%02d'%(kind,i),None)
        collection.objects.link(anchor);anchor.location=position;anchors.append(anchor)
for index,objects in enumerate(lods):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects+anchors:ob.hide_set(False);ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(out/('WC01_Landing_LOD%d.fbx'%index)),use_selection=True,object_types={'MESH','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',global_scale=1,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,use_custom_props=False)
    report['lods'].append({'level':index,'triangles':triangles(objects),'renderers':len(objects)})
    if index:
        for ob in objects:ob.hide_render=True;ob.hide_set(True)

scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=64
scene.world=bpy.data.worlds.new('Landing studio');scene.world.use_nodes=True
world_background=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
world_background.inputs[0].default_value=(.055,.075,.095,1)
world_background.inputs[1].default_value=.4
for name,loc,power,size in [('Key',(-12,-17,29),3300,12),('Fill',(14,-6,18),2300,11),('Rim',(4,12,23),4000,10)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    ob=bpy.data.objects.new(name,data);scene.collection.objects.link(ob);ob.location=loc
    ob.rotation_euler=(Vector((0,0,11))-ob.location).to_track_quat('-Z','Y').to_euler()
camdata=bpy.data.cameras.new('Landing overview');camera=bpy.data.objects.new('Landing overview',camdata)
scene.collection.objects.link(camera);camera.location=(30,-48,29);camera.rotation_euler=(Vector((0,0,12))-camera.location).to_track_quat('-Z','Y').to_euler()
camdata.type='ORTHO';camdata.ortho_scale=29;scene.camera=camera
scene.render.resolution_x=1200;scene.render.resolution_y=1600;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
bpy.ops.wm.save_as_mainfile(filepath=str(out/'文昌一号_迫降游戏版.blend'))
(out/'model-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
assert hashlib.sha256(src.read_bytes()).hexdigest()==source_hash
print('WC01_GAME_MODEL_COMPLETE',json.dumps(report,ensure_ascii=False),flush=True)
scene.render.filepath=str(out/'model-preview.png');bpy.ops.render.render(write_still=True)
