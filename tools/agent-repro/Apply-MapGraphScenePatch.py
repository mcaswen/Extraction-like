"""Apply only the verified command-map binding delta to the source Unity scene."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import tempfile

SCENE = 'Assets/Scenes/Scene_DB/Scenezl_Final 1.unity'
ASSET = 'Assets/SO/MapGraph/SO_MapGraphDefinition_Scenezl_Final1.asset'
NAV_PREFIX = 'Assets/Scenes/Scene_DB/Scenezl_Final 1/'
HEADER = re.compile(r'^--- !u!(\d+) &(-?\d+)( stripped)?\r?$', re.M)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def split_scene(data):
    text = data.decode('utf-8')
    matches = list(HEADER.finditer(text))
    require(matches, 'Missing Unity YAML document headers')
    parts = {}
    for i, match in enumerate(matches):
        identity = match.group(2)
        require(identity not in parts, 'Duplicate scene object ID: ' + identity)
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        parts[identity] = (int(match.group(1)), bool(match.group(3)), text[match.start():end])
    return text[:matches[0].start()], parts


def clean_added(text):
    return '\n'.join(line.rstrip() for line in text.splitlines()) + '\n'


def field_ref(text, name):
    found = re.search(r'^\s*' + re.escape(name) + r': \{fileID: (-?\d+)\}', text, re.M)
    require(found is not None, 'Missing local reference: ' + name)
    return found.group(1)


def merge_binding(base, saved, binding_guid, asset_guid):
    prefix, old = split_scene(base)
    _, new = split_scene(saved)
    require(set(old).issubset(new), 'Saving unexpectedly removed original scene objects')
    binding_ids = [identity for identity, (kind, stripped, body) in new.items()
                   if kind == 114 and not stripped and re.search(r'm_Script: \{[^\n]*guid: ' + binding_guid, body)]
    require(len(binding_ids) == 1, 'Expected exactly one command-map binding')
    binding_id = binding_ids[0]
    require(binding_id not in old, 'Initial installation refuses to replace an existing binding')
    binding = new[binding_id][2]
    require(re.search(r'_mapDefinition: \{[^\n]*guid: ' + asset_guid, binding), 'Binding references another graph asset')
    game_object = field_ref(binding, 'm_GameObject')
    require(game_object not in old and new[game_object][0] == 1, 'Binding root must be a new GameObject')
    root = new[game_object][2]
    require(re.search(r'^  m_Name: MapGraphBinding\r?$', root, re.M), 'Unexpected root object name')
    components = re.findall(r'^  - component: \{fileID: (-?\d+)\}', root, re.M)
    transform_ids = [identity for identity in components if new[identity][0] == 4]
    require(len(components) == 2 and binding_id in components and len(transform_ids) == 1, 'Unexpected root components')
    transform_id = transform_ids[0]
    require(field_ref(new[transform_id][2], 'm_Father') == '0', 'Binding is not a scene root')
    target_ids = set(re.findall(r'^\s+_(?:target|zone): \{fileID: (-?\d+)\}', binding, re.M)) - {'0'}
    additions = set(new) - set(old)
    ordinary = {binding_id, game_object, transform_id}
    for identity in additions - ordinary:
        kind, stripped, body = new[identity]
        require(kind == 114 and stripped and identity in target_ids, 'Unexpected added scene object: ' + identity)
        prefab_instance = field_ref(body, 'm_PrefabInstance')
        require(prefab_instance in old and old[prefab_instance][0] == 1001, 'New reference is not from an existing prefab instance')
    require(target_ids.issubset(set(old) | additions), 'Binding has dangling target references')
    roots = [identity for identity, (kind, _, _) in old.items() if kind == 1660057539]
    require(len(roots) == 1, 'Expected one SceneRoots document')
    roots_id = roots[0]
    old_roots = re.findall(r'^  - \{fileID: (-?\d+)\}', old[roots_id][2], re.M)
    saved_roots = re.findall(r'^  - \{fileID: (-?\d+)\}', new[roots_id][2], re.M)
    require(saved_roots == old_roots + [transform_id], 'Saving changed other root ordering or membership')
    merged = prefix
    ignored = []
    for identity, (_, _, body) in old.items():
        if identity == roots_id:
            merged += body.rstrip('\r\n') + '\n  - {fileID: ' + transform_id + '}\n'
        else:
            merged += body
            if body != new[identity][2]:
                ignored.append(identity)
    for identity in new:
        if identity in additions:
            merged += clean_added(new[identity][2])
    _, check = split_scene(merged.encode('utf-8'))
    require(all(check[identity] == old[identity] for identity in old if identity != roots_id), 'Protected source documents changed')
    return merged.encode('utf-8'), {'addedIds': sorted(additions), 'bindingId': binding_id, 'rootTransformId': transform_id,
                                  'targetReferences': len(target_ids), 'ignoredSerializationChanges': ignored, 'changedOriginalIds': [roots_id]}


def atomic_write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary = tempfile.mkstemp(prefix='.map-install-', dir=str(path.parent))
    try:
        with os.fdopen(handle, 'wb') as stream:
            stream.write(data)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', default='.')
    parser.add_argument('--run', required=True)
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    source, run = Path(args.source).resolve(), Path(args.run).resolve()
    evidence = json.loads((run / 'result.json').read_text(encoding='utf-8-sig'))
    process = json.loads((run / 'process-result.json').read_text(encoding='utf-8-sig'))
    manifest = json.loads((run / 'manifest.json').read_text(encoding='utf-8-sig'))
    require(evidence.get('passed') and process.get('exitCode') == 0, 'Installation fixture did not pass')
    require(evidence['scenePath'] == SCENE and evidence['assetPath'] == ASSET, 'Unexpected scene or asset path')
    require(evidence['nodes'] == 28 and evidence['zones'] == 7 and evidence['pendingQueries'] == 0, 'Incomplete binding evidence')
    scene, asset = source / SCENE, source / ASSET
    require(not asset.exists() and not Path(str(asset) + '.meta').exists(), 'Initial installation refuses an existing asset')
    base = (run / 'base-scene.unity').read_bytes()
    require(scene.read_bytes() == base, 'Source scene changed after generation')
    for item in manifest['files']:
        if item['path'] == SCENE + '.meta' or item['path'].startswith(NAV_PREFIX):
            digest = hashlib.sha256((source / item['path']).read_bytes()).hexdigest()
            require(digest.lower() == item['sha256'].lower(), 'Source navigation input changed: ' + item['path'])
    binding_meta = (source / 'Assets/Scripts/Gameplay/MapGraph/Binding/MapGraphBindingAuthoring.cs.meta').read_text(encoding='utf-8')
    binding_guid = re.search(r'^guid: ([0-9a-f]+)', binding_meta, re.M).group(1)
    merged, report = merge_binding(base, (run / 'saved-scene.unity').read_bytes(), binding_guid, evidence['assetGuid'])
    report.update({'scene': SCENE, 'asset': ASSET, 'sourceBeforeSha256': hashlib.sha256(base).hexdigest(),
                   'sourceAfterSha256': hashlib.sha256(merged).hexdigest(), 'applied': False})
    (run / 'proposed-source-scene.unity').write_bytes(merged)
    if args.apply:
        require(scene.read_bytes() == base, 'Source scene changed while reviewing patch')
        try:
            atomic_write(asset, (run / 'map.asset').read_bytes())
            atomic_write(Path(str(asset) + '.meta'), clean_added((run / 'map.asset.meta').read_text(encoding='utf-8-sig')).encode('utf-8'))
            atomic_write(scene, merged)
            report['applied'] = True
        except Exception:
            atomic_write(scene, base)
            for created in (asset, Path(str(asset) + '.meta')):
                if created.exists():
                    created.unlink()
            raise
    (run / 'source-patch-report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
