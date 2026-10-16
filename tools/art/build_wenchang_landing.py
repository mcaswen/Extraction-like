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
    'Hull': ((.70,.73,.70), .08, .60),
    'Trim': ((.34,.42,.45), .28, .52),
    'Orange': ((.78,.245,.065), .08, .55),
    'Steel': ((.38,.46,.48), .72, .36),
    'Dark': ((.043,.065,.078), .28, .61),
    'WarmMetal': ((.44,.29,.15), .65, .47),
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
        if edges:bmesh.ops.subdivide_edges(bm,edges=edges,cuts=4,use_grid_fill=True)
        bm.to_mesh(mesh);bm.free()
        for v in mesh.vertices:
            a=math.atan2(v.co.y,v.co.x);da=math.atan2(math.sin(a+.62),math.cos(a+.62))
            w=math.exp(-((v.co.z-6.1)/1.4)**2-(da/.31)**2)
            v.co.x*=1-.10*w;v.co.y*=1-.10*w
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

# Four deliberate, readable landing legs. The nozzle stays clear of the ground.
feet=[]
for i in range(4):
    a=math.pi/4+i*math.pi/2;u=Vector((math.cos(a),math.sin(a),0));t=Vector((-u.y,u.x,0))
    foot=u*4.65;foot.z=.18;feet.append(tuple(foot))
    upper=u*1.92;upper.z=5.35
    knee=u*3.4;knee.z=1.65
    rod('Landing leg %d • titanium outer strut'%i,upper,knee,.19,'Dark',12)
    rod('Landing leg %d • piston barrel'%i,upper+u*.18-Vector((0,0,.65)),foot+Vector((0,0,.7)),.13,'Trim',12)
    rod('Landing leg %d • exposed damper'%i,knee,foot+Vector((0,0,.2)),.095,'Steel',12)
    for sign in [-1,1]:
        rod('Landing leg %d • diagonal brace'%i,u*1.85+t*.38*sign+Vector((0,0,3.1)),knee+t*.23*sign,.072,'Steel',10)
        rod('Landing leg %d • upper hinge'%i,upper+t*.36*sign,upper+t*.50*sign,.23,'Steel',16)
    box('Landing foot %d • load-spreading pad'%i,foot,(1.65,1.18,.28),'Dark',a, .065)
    box('Landing foot %d • upper plate'%i,foot+Vector((0,0,.17)),(1.38,.95,.07),'Trim',a,.02)
    for sign in [-1,1]:
        box('Landing foot %d • orange hazard edge'%i,foot+t*.43*sign+Vector((0,0,.23)),(1.18,.10,.035),'Orange',a,.006)

# A torn outer service shield: shallow, not a hole through the propellant tank.
a=-.65;r=2.10;u=Vector((math.cos(a),math.sin(a),0));t=Vector((-u.y,u.x,0));c=u*r+Vector((0,0,6.25))
vs=[c+t*x+Vector((0,0,z))+u*lift for x,z,lift in [(-.61,-.83,.01),(.61,-.83,.01),(.61,.47,.02),(.28,.88,.20),(-.33,.75,.33),(-.61,.45,.08)]]
panel=add_mesh('Damaged service shield • peeled insulated edge',vs,[(0,1,2,3,4,5)],'Trim')
sol=panel.modifiers.new('Shield thickness','SOLIDIFY');sol.thickness=.045;apply_modifier(panel,sol)
back=[c+t*x+Vector((0,0,z))-u*.05 for x,z in [(-.55,-.70),(.55,-.70),(.55,.65),(-.55,.65)]]
add_mesh('Recessed access bay • dark backing',back,[(0,1,2,3)],'Dark')
for k in range(3):
    rod('Access bay • exposed protected conduit',c+t*(-.36+k*.22)+Vector((0,0,-.62)),c+t*(-.29+k*.24)+Vector((0,0,.59)),.032,'WarmMetal',8)

def paint(ob):
    key=ob['surface_class'];base=Vector(PALETTE[key][0]);me=ob.data
    for attr in list(me.color_attributes):me.color_attributes.remove(attr)
    colors=me.color_attributes.new(name='LandingPaint',type='FLOAT_COLOR',domain='POINT')
    for v,item in zip(me.vertices,colors.data):
        p=v.co;z=p.z-1.18;a=math.atan2(p.y,p.x)
        noise=.5+.25*math.sin(p.x*9.2+p.z*6.6)+.25*math.sin(p.y*13.5-p.z*4.7)
        soot=max(0,1-z/4.8)*(.34+.16*noise)
        da=math.atan2(math.sin(a+.65),math.cos(a+.65))
        streak=math.exp(-(da/.25)**2-((z-6.4)/2.1)**2)*.55
        dirt=min(.66,max(0,soot+streak)) if key in ['Hull','Trim','Orange','Steel','Dark'] else 0
        if ob.get('identity_artwork'):dirt=0
        c=base.lerp(Vector((.085,.078,.063)),dirt)
        # Broad edge highlights survive the normal gameplay camera; no random speckle noise.
        if key in ['Hull','Trim','Orange'] and not ob.get('identity_artwork'):
            c*=.98+.025*math.sin(z*3.1)
        item.color=(*c,1)
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

report={'source_sha256':source_hash,'intact_master_unchanged':True,'feet_blender':feet,
        'smoke_anchors_blender':[[1.70,-1.30,6.6],[-3.18,-.69,3.0]],
        'lods':[],'material_settings':{k:{'metallic':v[1],'roughness':v[2]} for k,v in PALETTE.items()},
        'removed_subpixel_objects':len(omitted)}
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

scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32
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
