"""Builds PlayForge's documentation site into docfx/_site.

1. Copies the assemblies the package compiles against into obj/refs: Unity's own, from its install, and the packages',
   from this project's Library, so the API reference can read the package's source as Unity compiles it.
2. Runs DocFX's metadata step: a page for each public type of the runtime, the Creator Lab, the editor and the test kit.
3. Runs prepare.py: the guides split into a page per section, each type a guide names linked to its page, each type's
   page listing the guide sections that name it, and the API's contents grouped by area.
4. Runs DocFX's build step.

Run from anywhere: python docfx/build.py, with --serve to look at the site on http://localhost:8080 after. Unity's
install is taken from UNITY_EDITOR_DATA when set, else from the Hub's default folder for the version the project uses;
the packages' assemblies from PLAYFORGE_LIBRARY when set, as publish.sh sets it for a copy of the repository, else from
the project's own Library.
"""
import glob
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
REFS = os.path.join(HERE, 'obj', 'refs')


def unity_version():
    with open(os.path.join(REPO, 'ProjectSettings', 'ProjectVersion.txt'), encoding='utf-8') as f:
        for line in f:
            if line.startswith('m_EditorVersion:'):
                return line.split(':', 1)[1].strip()
    raise SystemExit('ProjectSettings/ProjectVersion.txt names no editor version')


def unity_data():
    data = os.environ.get('UNITY_EDITOR_DATA')
    if not data:
        data = os.path.join('C:/Program Files/Unity/Hub/Editor', unity_version(), 'Editor', 'Data')
    if not os.path.isdir(os.path.join(data, 'Managed', 'UnityEngine')):
        raise SystemExit('No Unity install at %s: set UNITY_EDITOR_DATA to its Editor/Data folder' % data)
    return data


# The packages the package's assemblies reference, as this project's Library compiles them
PACKAGE_ASSEMBLIES = ('UniTask', 'UnityEngine.UI', 'Unity.TextMeshPro', 'Unity.Profiling.Core', 'Unity.InputSystem',
                      'UnityEngine.TestRunner')
NUNIT = os.path.join('PackageCache', 'com.unity.ext.nunit', 'net40', 'unity-custom', 'nunit.framework.dll')


def copy_refs():
    data = unity_data()
    sources = glob.glob(os.path.join(data, 'Managed', 'UnityEngine', '*.dll'))
    sources.append(os.path.join(data, 'Managed', 'UnityEditor.Graphs.dll'))
    library = os.environ.get('PLAYFORGE_LIBRARY') or os.path.join(REPO, 'Library')
    sources += [os.path.join(library, 'ScriptAssemblies', name + '.dll') for name in PACKAGE_ASSEMBLIES]
    sources.append(os.path.join(library, NUNIT))
    missing = [path for path in sources if not os.path.isfile(path)]
    if missing:
        raise SystemExit('Missing, so open the project in Unity once to compile its packages:\n  ' + '\n  '.join(missing))
    os.makedirs(REFS, exist_ok=True)
    for path in sources:
        target = os.path.join(REFS, os.path.basename(path))
        if not os.path.isfile(target) or os.path.getmtime(target) < os.path.getmtime(path):
            shutil.copy2(path, target)
    print('References: %d assemblies in %s' % (len(sources), os.path.relpath(REFS, REPO)))


def docfx(*args):
    subprocess.run(['dotnet', 'docfx'] + list(args), cwd=REPO, check=True)


def main():
    subprocess.run(['dotnet', 'tool', 'restore'], cwd=REPO, check=True, stdout=subprocess.DEVNULL)
    copy_refs()
    shutil.rmtree(os.path.join(HERE, 'obj', 'api'), ignore_errors=True)
    docfx('metadata', 'docfx/docfx.json')
    subprocess.run([sys.executable, '-I', os.path.join(HERE, 'prepare.py')], cwd=REPO, check=True)
    docfx('build', 'docfx/docfx.json', *(['--serve'] if '--serve' in sys.argv else []))


if __name__ == '__main__':
    main()
