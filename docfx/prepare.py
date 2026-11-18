"""Prepares the documentation site's generated pages, between DocFX's metadata step and its build. build.py runs it.

- The guides: each guide the package ships, Assets/PlayForge/Documentation/<NAME>.md, becomes a folder of pages under
  guides/: index.md, its opening and a list of its sections, and a page for each of its ## sections. Links between the
  guides go to the page holding the section they name, and the contents (guides/toc.yml) list each guide's sections.
- Guides to the API: a type named in backticks, as `GameplayEffect` or `ForgeLog.MinimumLevel`, links to its page,
  once on each page. Each guide's opening page lists the types of its area.
- The API to the guides: each type's page says which guide sections name it (apidoc/, DocFX overwrite files), or, named
  in none, which guide covers its area.
- The API reference: DocFX's metadata (obj/api) copied to api/, each item given its assembly, from where its source is,
  and no links to the source; the contents grouped by area (api/toc.yml); its opening page, api/index.md, from api.md
  and the areas.

Everything it writes is generated, and git ignores it: guides/, api/ and apidoc/.
"""
import collections
import io
import os
import re
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
DOCS = os.path.join(REPO, 'Assets', 'PlayForge', 'Documentation')
RAW_API = os.path.join(HERE, 'obj', 'api')
API = os.path.join(HERE, 'api')
GUIDES = os.path.join(HERE, 'guides')
APIDOC = os.path.join(HERE, 'apidoc')

# The guides in the contents' order, by group: each by its file's name under Documentation. Every guide the package
# ships is listed here; a group of one guide is that guide.
GUIDE_GROUPS = [
    ('Getting Started', ['GETTING_STARTED']),
    ('Gameplay', ['ENTITIES', 'ATTRIBUTES', 'ATTRIBUTE_SETS', 'EFFECTS', 'EFFECT_PIPELINE', 'VALUE_PIPELINES', 'SCALERS',
                  'ABILITIES', 'ITEMS', 'TAGS', 'CUES', 'WORKERS']),
    ('Running it', ['PROCESSES', 'TASK_SEQUENCES', 'SERVICES', 'SAVING', 'SPREADSHEETS']),
    ('Tools', ['EDITOR', 'CREATOR_LAB', 'TESTING']),
    ('Going further', ['EXTENDING', 'RECIPES']),
]
# The contents' name for a guide whose title is longer than the contents want; the rest go by their titles
SHORT_NAMES = {'EFFECTS': 'Effects', 'EFFECT_PIPELINE': 'Effect & Attribute Pipeline',
               'SERVICES': 'Time, Input and Logging'}

# Each guide's page of the kinds PlayForge ships for its pickers, after its sections: a table for each family, every
# concrete class made from the type it names or implementing it, with its menu and what it does, as its [ForgeMenu]
# says, or else its summary's first sentence. A class is in the family of the page whose type is nearest it.
CATALOG_TITLE = 'Built-in kinds'
CATALOGS = [
    ('ABILITIES', [('Components', 'AbstractAbilityComponent'), ('Tasks', 'AbstractAbilityTask'),
                   ('Targeting tasks', 'AbstractTargetingAbilityTask'), ('Requirements', 'IAbilityValidationRule'),
                   ('Charge recharges', 'AbstractChargeRecharge'), ('Stage policies', 'IAbilityProxyStagePolicy')]),
    ('EFFECTS', [('Components', 'AbstractEffectComponent'), ('Changes', 'AbstractEffectChange'),
                 ('Stacking', 'AbstractStackingConfig'), ('Purge filters', 'AbstractPurgeFilter')]),
    ('ENTITIES', [('Components', 'AbstractEntityComponent'), ('Respawn places', 'AbstractRespawnPlace'),
                  ('Local Data kinds', 'LocalDataValue')]),
    ('ITEMS', [('Components', 'AbstractItemComponent')]),
    ('WORKERS', [('Attribute workers', 'AbstractAttributeWorker'), ('Impact workers', 'AbstractImpactWorker'),
                 ('Effect workers', 'AbstractEffectWorker'), ('Tag workers', 'AbstractTagWorker'),
                 ('Analysis workers', 'AbstractAnalysisWorker'), ('Actions', 'IRootAction')]),
    ('CUES', [('Cue handlers', 'AbstractCueHandler')]),
    ('SCALERS', [('Scalers', 'AbstractScaler'), ('Number kinds', 'NumberKind'), ('Variances', 'AbstractVariance'),
                 ('Scaler behaviours', 'AbstractScalerBehaviour'), ('Conditions', 'GameplayCondition')]),
    ('VALUE_PIPELINES', [('Stages', 'AbstractValueStage')]),
    ('TASK_SEQUENCES', [('Injections', 'ISequenceInjection'), ('Stage policies', 'ISequenceStagePolicy'),
                        ('Clock choices', 'AbstractClockChoice')]),
]

# The API's areas, by where a type's source is under Assets/PlayForge, the first match winning: each area's part of the
# reference, its name, and the guide that covers it
AREAS = [
    ('Runtime/PlayForge/GAS/ActionQueue/', 'Runtime', 'Action Queue', 'WORKERS'),
    ('Runtime/PlayForge/GAS/', 'Runtime', 'Entities', 'ENTITIES'),
    ('Runtime/PlayForge/LevelComponent/', 'Runtime', 'Entities', 'ENTITIES'),
    ('Runtime/PlayForge/AttributeSystem/', 'Runtime', 'Attributes', 'ATTRIBUTES'),
    ('Runtime/PlayForge/GameplayEffect/Cues/', 'Runtime', 'Cues', 'CUES'),
    ('Runtime/PlayForge/GameplayEffect/', 'Runtime', 'Effects', 'EFFECTS'),
    ('Runtime/PlayForge/AbilitySystem/Tasks/', 'Runtime', 'Ability Tasks', 'ABILITIES'),
    ('Runtime/PlayForge/CompositeBehaviour/', 'Runtime', 'Ability Tasks', 'ABILITIES'),
    ('Runtime/PlayForge/AbilitySystem/', 'Runtime', 'Abilities', 'ABILITIES'),
    ('Runtime/PlayForge/ItemSystem/', 'Runtime', 'Items', 'ITEMS'),
    ('Runtime/PlayForge/Workers/', 'Runtime', 'Workers', 'WORKERS'),
    ('Runtime/PlayForge/Tag/', 'Runtime', 'Tags', 'TAGS'),
    ('Runtime/PlayForge/Numbers/Scalers/', 'Runtime', 'Scalers', 'SCALERS'),
    ('Runtime/PlayForge/Numbers/Pipelines/', 'Runtime', 'Value Pipelines', 'VALUE_PIPELINES'),
    ('Runtime/PlayForge/Numbers/', 'Runtime', 'Numbers and Conditions', 'SCALERS'),
    ('Runtime/PlayForge/Management/', 'Runtime', 'Processes', 'PROCESSES'),
    ('Runtime/PlayForge/Core/TaskSequence/', 'Runtime', 'Task Sequences', 'TASK_SEQUENCES'),
    ('Runtime/PlayForge/Save/', 'Runtime', 'Saving and Projects', 'SAVING'),
    ('Runtime/PlayForge/Core/Attributes/', 'Runtime', 'Inspector Attributes', 'EXTENDING'),
    ('Runtime/PlayForge/Core/', 'Runtime', 'Assets and Components', 'EXTENDING'),
    ('Runtime/PlayForge/Readers/', 'Runtime', 'Assets and Components', 'EXTENDING'),
    ('Runtime/PlayForge/Utils/', 'Runtime', 'Time, Randomness and Input', 'SERVICES'),
    ('Runtime/Lab/', 'Runtime', 'The Creator Lab', 'CREATOR_LAB'),
    ('Editor/Lab/', 'Editor', 'The Creator Lab', 'CREATOR_LAB'),
    ('Editor/TheForge/', 'Editor', 'The Forge and Projects', 'EDITOR'),
    ('Editor/Projects/', 'Editor', 'The Forge and Projects', 'EDITOR'),
    ('Editor/Windows/', 'Editor', 'The Forge and Projects', 'EDITOR'),
    ('Editor/Tools/', 'Editor', 'The Forge and Projects', 'EDITOR'),
    ('Editor/Problems/', 'Editor', 'Problems and Validation', 'EDITOR'),
    ('Editor/Debugger/', 'Editor', 'Debugger and Profiler', 'EDITOR'),
    ('Editor/Settings/', 'Editor', 'Settings', 'EDITOR'),
    ('Editor/Spreadsheets/', 'Editor', 'Spreadsheets', 'SPREADSHEETS'),
    ('Editor/Inspectors/', 'Editor', 'Inspectors', 'EDITOR'),
    ('Editor/Drawers/Tag/', 'Editor', 'Tags', 'TAGS'),
    ('Editor/Drawers/', 'Editor', 'Drawers', 'EDITOR'),
    ('Editor/Numbers/', 'Editor', 'Number Fields', 'SCALERS'),
    ('Editor/UI/', 'Editor', 'Editor UI Kit', 'EDITOR'),
    ('Editor/', 'Editor', 'Editor Utilities', 'EDITOR'),
    ('TestKit/', 'Test Kit', 'Test Kit', 'TESTING'),
]
PARTS = ['Runtime', 'Editor', 'Test Kit']
# The order each part of the API's contents lists its areas in; an area not named here comes after them
AREA_ORDER = {
    'Runtime': ['Entities', 'Attributes', 'Effects', 'Abilities', 'Ability Tasks', 'Items', 'Tags', 'Cues', 'Workers',
                'Action Queue', 'Scalers', 'Value Pipelines', 'Numbers and Conditions', 'Processes', 'Task Sequences',
                'Saving and Projects', 'Time, Randomness and Input', 'Assets and Components', 'Inspector Attributes',
                'The Creator Lab'],
    'Editor': ['The Forge and Projects', 'Inspectors', 'Drawers', 'Tags', 'Number Fields', 'Problems and Validation',
               'Debugger and Profiler', 'Settings', 'Spreadsheets', 'The Creator Lab', 'Editor UI Kit',
               'Editor Utilities'],
}

# Each item's assembly, by where its source is: the package's assembly definitions
ASSEMBLIES = [
    ('Runtime/Lab/', 'FarEmerald.PlayForge.Lab'),
    ('Runtime/', 'FarEmerald.PlayForge'),
    ('Editor/Lab/', 'FarEmerald.PlayForge.Lab.Editor'),
    ('Editor/', 'FarEmerald.PlayForge.Editor'),
    ('TestKit/', 'FarEmerald.PlayForge.Testing'),
]

# How many guide sections a type's page lists before it says how many more name it
MOST_SECTIONS = 12

problems = []


def problem(text):
    problems.append(text)


# ---------------------------------------------------------------------------------------------------------- Markdown

HEADING = re.compile(r'^(#{1,6})\s+(.*?)\s*#*\s*$')
FENCE = re.compile(r'^\s*(```|~~~)')
LINK = re.compile(r'\[((?:[^\[\]]|\[[^\[\]]*\])*)\]\(([^)\s]+)\)')
CODE_SPAN = re.compile(r'(?<!`)`([^`\n]+)`(?!`)')


def plain(text):
    """A heading's text as the reader sees it: links to their text, no code or emphasis marks."""
    text = LINK.sub(lambda m: m.group(1), text)
    return text.replace('`', '').replace('**', '').replace('*', '')


def slug(text):
    """A heading's anchor, GitHub's way, as the site and ForgeDocs name it: lowercase, each space a hyphen, other
    punctuation dropped."""
    out = []
    for c in plain(text).strip().lower():
        if c.isalnum() or c in '-_':
            out.append(c)
        elif c == ' ':
            out.append('-')
    return ''.join(out)


def code_mask(lines):
    """For each line, whether it's in a fenced code block, its fences included."""
    mask, inside = [], False
    for line in lines:
        if FENCE.match(line):
            mask.append(True)
            inside = not inside
        else:
            mask.append(inside)
    return mask


class Anchors:
    """A page's heading anchors as GitHub and the site give them: a repeated heading's anchor numbered from -1."""

    def __init__(self):
        self.seen = collections.Counter()

    def take(self, text):
        base = slug(text)
        n = self.seen[base]
        self.seen[base] += 1
        return base if n == 0 else '%s-%d' % (base, n)


class Section:
    def __init__(self, title, lines):
        self.title, self.lines = title, lines
        self.slug = ''
        self.anchors = {}   # the anchor of each heading in it on its own page, by the heading's anchor in the guide
        self.titles = {}    # each heading's text, by its anchor in the guide


class Guide:
    def __init__(self, key):
        self.key = key
        self.slug = key.lower().replace('_', '-')
        with io.open(os.path.join(DOCS, key + '.md'), encoding='utf-8-sig') as f:
            lines = f.read().replace('\r\n', '\n').split('\n')
        mask = code_mask(lines)
        title = next((i for i, line in enumerate(lines) if line.startswith('# ')), None)
        if title is None:
            raise SystemExit('%s.md has no title' % key)
        self.title = re.sub(r'^PlayForge\s+[—-]\s+', '', lines[title][2:].strip())
        self.name = SHORT_NAMES.get(key, self.title)
        self.intro, self.sections = [], []
        guide_anchors = Anchors()
        self.anchor_to = {guide_anchors.take(lines[title][2:]): (None, None)}
        current = None
        for i in range(title + 1, len(lines)):
            line = lines[i]
            heading = None if mask[i] else HEADING.match(line)
            if heading and len(heading.group(1)) == 2:
                current = Section(heading.group(2), [])
                self.sections.append(current)
                current.page_anchors = Anchors()
                current.page_anchors.take(current.title)
                current.guide_anchor = guide_anchors.take(current.title)
                continue
            if heading and current is not None:
                anchor = guide_anchors.take(heading.group(2))
                current.anchors[anchor] = current.page_anchors.take(heading.group(2))
                current.titles[anchor] = plain(heading.group(2))
            elif heading:
                guide_anchors.take(heading.group(2))
            (current.lines if current is not None else self.intro).append(line)
        taken = set()
        for n, section in enumerate(self.sections):
            section.slug = slug(section.title) or 'section-%d' % (n + 1)
            if section.slug in taken or section.slug == 'index':
                raise SystemExit('%s.md: two sections would be the page %s.md' % (key, section.slug))
            taken.add(section.slug)
            self.anchor_to[section.guide_anchor] = (section, None)
            for anchor, page_anchor in section.anchors.items():
                self.anchor_to[anchor] = (section, page_anchor)

    def uid(self, section=None):
        return 'guide.' + self.slug + ('' if section is None else '.' + section.slug)


# --------------------------------------------------------------------------------------------------------------- API

class Item:
    def __init__(self, uid, fields):
        self.uid = uid
        self.name = fields.get('name', uid)
        self.kind = fields.get('type', '')
        self.parent = fields.get('parent')
        self.overload = fields.get('overload')
        self.path = fields.get('path')
        self.area = None
        self.bases, self.interfaces = [], []
        self.declaration = self.summary = ''


def unquote(value):
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] == "'":
        return value[1:-1].replace("''", "'")
    if len(value) >= 2 and value[0] == value[-1] == '"':
        return value[1:-1].replace('\\"', '"').replace('\\\\', '\\')
    return value


ITEM_FIELD = re.compile(r'^  (name|type|parent|overload): (.*)$', re.M)
SOURCE_BLOCK = re.compile(r'^  source:\n(?:    .*\n)+', re.M)
SOURCE_PATH = re.compile(r'^    path: (.*)$', re.M)
PACKAGE_PATH = re.compile(r'(?:\.\./)?Assets/PlayForge/(.*)$')


SYNTAX = re.compile(r'^  syntax:\n    content: (.*)$', re.M)
XREF_TAG = re.compile(r'<xref href="([^"]*)"[^>]*>(?:</xref>)?')


def declaration_of(chunk):
    """An item's declaration as its page shows it, its attributes before it: every line of it, as one."""
    match = SYNTAX.search(chunk)
    if not match:
        return ''
    if match.group(1).strip() not in ('>-', '|-', '>', '|'):
        return unquote(match.group(1))
    lines = []
    for line in chunk[match.end() + 1:].split('\n'):
        if line.startswith('      ') or not line.strip():
            lines.append(line.strip())
        else:
            break
    return ' '.join(line for line in lines if line)


def LIST_FIELD(name, chunk):
    """An item's list field, as its inheritance: the uids under it."""
    match = re.search(r'^  %s:\n((?:  - .*\n)+)' % name, chunk, re.M)
    return [unquote(line.strip()[2:]) for line in match.group(1).splitlines()] if match else []


def summary_of(chunk):
    """An item's summary as plain text: its comment's words, a reference as its name, no tags."""
    match = re.search(r'^  summary: (.*)$', chunk, re.M)
    if not match:
        return ''
    if match.group(1).strip() in ('>-', '|-', '>', '|'):
        lines = []
        for line in chunk[match.end() + 1:].split('\n'):
            if line.startswith('    ') or not line.strip():
                lines.append(line.strip())
            else:
                break
        text = ' '.join(line for line in lines if line)
    else:
        text = unquote(match.group(1))
    text = XREF_TAG.sub(lambda m: re.sub(r'\(.*$', '', unquote(m.group(1))).split('.')[-1].replace('%60', '`'), text)
    text = re.sub(r'<[^>]+>', '', text).replace('&lt;', '<').replace('&gt;', '>').replace('&amp;', '&')
    return re.sub(r'\s+', ' ', text).strip()


def assembly_of(path):
    return next((name for prefix, name in ASSEMBLIES if path.startswith(prefix)), None)


def area_of(path):
    return next(((part, area, guide) for prefix, part, area, guide in AREAS if path.startswith(prefix)), None)


def read_api():
    """The API's items, from DocFX's metadata, each given its assembly and its links to the source taken out."""
    items = {}
    shutil.rmtree(API, ignore_errors=True)
    os.makedirs(API)
    for name in sorted(os.listdir(RAW_API)):
        if not name.endswith('.yml') or name == 'toc.yml':
            continue
        with io.open(os.path.join(RAW_API, name), encoding='utf-8') as f:
            text = f.read()
        path = os.path.join(API, name)
        if not text.startswith('### YamlMime:ManagedReference'):
            write(path, text)
            continue
        body, _, references = text.partition('\nreferences:\n')
        head, _, listing = body.partition('\nitems:\n')
        chunks = re.split(r'^(?=- uid: )', listing, flags=re.M)
        out = []
        for chunk in chunks:
            if not chunk.startswith('- uid: '):
                out.append(chunk)
                continue
            uid = unquote(chunk.split('\n', 1)[0][len('- uid: '):])
            fields = {m.group(1): unquote(m.group(2)) for m in ITEM_FIELD.finditer(chunk)}
            source = SOURCE_BLOCK.search(chunk)
            if source:
                local = SOURCE_PATH.search(source.group(0))
                package_path = PACKAGE_PATH.search(local.group(1).strip()) if local else None
                fields['path'] = package_path.group(1) if package_path else None
                chunk = chunk[:source.start()] + chunk[source.end():]
            item = Item(uid, fields)
            item.bases = LIST_FIELD('inheritance', chunk)
            item.interfaces = LIST_FIELD('implements', chunk)
            item.declaration = declaration_of(chunk)
            item.summary = summary_of(chunk)
            items[uid] = item
            assembly = assembly_of(item.path or '')
            if assembly and '\n  namespace: ' in chunk and '\n  assemblies:' not in chunk:
                chunk = chunk.replace('\n  namespace: ', '\n  assemblies:\n  - %s\n  namespace: ' % assembly, 1)
            out.append(chunk)
        text = head + '\nitems:\n' + ''.join(out) + ('\nreferences:\n' + references if references else '')
        with io.open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(text)
    types = [item for item in items.values() if item.kind in ('Class', 'Struct', 'Interface', 'Enum', 'Delegate')]
    for item in types:
        item.area = area_of(item.path or '')
        if item.area is None:
            problem('No area for %s (%s): add its folder to AREAS in prepare.py' % (item.uid, item.path))
    return items, types


class Names:
    """What a name in backticks can link to: a type by its name, or a member as Type.Member, where only one has it."""

    def __init__(self, items, types):
        self.uid_of, self.type_of = {}, {}
        ambiguous = set()

        def add(key, uid):
            if key in self.uid_of and self.uid_of[key] != uid:
                ambiguous.add(key)
            self.uid_of[key] = uid

        names_of = {}
        for item in types:
            keys = {item.name, item.name.split('.')[-1]}
            keys |= {re.sub(r'<[^<>]*>', '', key) for key in keys}
            names_of[item.uid] = {key for key in keys if key}
            for key in names_of[item.uid]:
                add(key, item.uid)
                self.type_of.setdefault(key, set()).add(item.uid)
        for item in items.values():
            if item.kind in ('Class', 'Struct', 'Interface', 'Enum', 'Delegate', 'Namespace', 'Constructor', 'Operator'):
                continue
            if item.parent not in names_of:
                continue
            member = re.sub(r'\(.*$', '', item.name).split('.')[-1]
            target = item.overload if item.kind == 'Method' and item.overload else item.uid
            for type_name in names_of[item.parent]:
                add(type_name + '.' + member, target)
                self.type_of.setdefault(type_name + '.' + member, set()).add(item.parent)
        # A member by its name alone, where one type has it and its name is of two words run together at least, as
        # WaitForKey: a name of one word, as Clear, is too common to say whose it is
        bare = collections.defaultdict(set)
        bare_type = collections.defaultdict(set)
        for item in items.values():
            if item.kind in ('Method', 'Property', 'Field', 'Event') and item.parent in names_of:
                member = re.sub(r'\(.*$', '', item.name).split('.')[-1]
                bare[member].add(item.overload if item.kind == 'Method' and item.overload else item.uid)
                bare_type[member].add(item.parent)
        for member, uids in bare.items():
            if len(uids) == 1 and member not in self.uid_of and re.fullmatch(r'[A-Z][a-z0-9]+(?:[A-Z][A-Za-z0-9]*)+',
                                                                              member):
                self.uid_of[member] = next(iter(uids))
                self.type_of.setdefault(member, set()).update(bare_type[member])
        for key in ambiguous:
            del self.uid_of[key]
        self.types = {item.uid: item for item in types}
        # The names prose may say without backticks: one type's, of two words run together at least, as GameplayEffect
        type_names = {key for uid in names_of for key in names_of[uid]}
        self.prose = {key: uids for key, uids in self.type_of.items()
                      if key in type_names and len(uids) == 1 and '.' not in key
                      and re.fullmatch(r'[A-Z][a-z0-9]+(?:[A-Z][A-Za-z0-9]*)+', key)}

    def link(self, span):
        """The uid a code span's text links to, if it names one thing: a type, or a member as Type.Member(...)."""
        text = span.strip()
        text = re.sub(r'\(.*\)$', '', text) if re.match(r'^[\w.<>]+\(.*\)$', text) else text
        return self.uid_of.get(text)

    def named(self, span):
        """The types a code span names."""
        text = re.sub(r'\(.*$', '', span.strip())
        found = set(self.type_of.get(text, ()))
        if not found and '.' in text:
            found = set(self.type_of.get(text.split('.')[0], ()))
        return {uid for uid in found if uid in self.types}


def xref(uid):
    return 'xref:' + uid.replace('`', '%60').replace(' ', '%20')


# ------------------------------------------------------------------------------------------------------------ Guides

def rewrite_links(guide, text, guides):
    """The guide's links to guides, its own sections among them, made the pages that hold them."""

    def target(m):
        label, href = m.group(1), m.group(2)
        page, _, anchor = href.partition('#')
        if re.match(r'^[a-z]+:', href):
            return m.group(0)
        if page and not page.endswith('.md'):
            return m.group(0)
        key = page[:-3] if page else guide.key
        other = guides.get(key)
        if other is None:
            problem('%s.md links to %s, which is no guide' % (guide.key, href))
            return m.group(0)
        section, page_anchor = (None, None)
        if anchor:
            if anchor not in other.anchor_to:
                problem('%s.md links to %s, a heading %s.md lacks' % (guide.key, href, other.key))
            section, page_anchor = other.anchor_to.get(anchor, (None, None))
        name = other.name + (': ' + plain(section.title) if section is not None else '')
        if page_anchor:
            name = other.name + ': ' + section.titles[anchor]
        if label.strip() == page or label.strip() == href:
            label = name
        folder = '' if other is guide else '../' + other.slug + '/'
        return '[%s](%s%s%s)' % (label, folder, section.slug + '.md' if section is not None else 'index.md',
                                 '#' + page_anchor if page_anchor else '')

    return LINK.sub(target, text)


def link_types(lines, names, linked):
    """The page's lines with each type or member named in backticks linked to its API page, once on the page, outside
    code, headings and links. Gives the types the page names."""
    mask = code_mask(lines)
    mentioned = collections.Counter()
    out = []
    for line, code in zip(lines, mask):
        if code:
            if not FENCE.match(line):
                for word in set(re.findall(r'\b[A-Z]\w+\b', line)):
                    for uid in names.named(word):
                        mentioned[uid] += 1
            out.append(line)
            continue
        if HEADING.match(line):
            for span in CODE_SPAN.findall(line):
                for uid in names.named(span):
                    mentioned[uid] += 3
            out.append(line)
            continue
        links = []

        def hide(m):
            links.append(m.group(0))
            for span in CODE_SPAN.findall(m.group(1)):
                for uid in names.named(span):
                    mentioned[uid] += 1
            return '\x00%d\x00' % (len(links) - 1)

        hidden = LINK.sub(hide, line)

        def span(m):
            for uid in names.named(m.group(1)):
                mentioned[uid] += 1
            uid = names.link(m.group(1))
            if uid is None or uid in linked:
                return m.group(0)
            linked.add(uid)
            return '[%s](%s)' % (m.group(0), xref(uid))

        hidden = CODE_SPAN.sub(span, hidden)
        prose = CODE_SPAN.sub('', hidden)
        for word in set(re.findall(r'\b[A-Z][A-Za-z0-9]+\b', prose)):
            for uid in names.prose.get(word, ()):
                mentioned[uid] += 1
        out.append(re.sub('\x00(\\d+)\x00', lambda m: links[int(m.group(1))], hidden))
    return out, mentioned


SOURCE = os.path.join(REPO, 'Assets', 'PlayForge')
FORGE_MENU = re.compile(r'\[ForgeMenu\(((?:[^()"]|"(?:[^"\\]|\\.)*")*)\)\]\s*(?:\[[^\]]*\]\s*)*'
                        r'(?:public|internal)\s+(?:(?:sealed|abstract|partial)\s+)*class\s+(\w+)')
STRING = r'"((?:[^"\\]|\\.)*)"'
STRINGS = r'"(?:[^"\\]|\\.)*"(?:\s*\+\s*"(?:[^"\\]|\\.)*")*'


def forge_menus():
    """Each class's [ForgeMenu]: its path, None where it names none; its name; its description."""
    menus = {}
    for folder in ('Runtime', 'Editor'):
        for root, _, files in os.walk(os.path.join(SOURCE, folder)):
            for name in files:
                if not name.endswith('.cs'):
                    continue
                with io.open(os.path.join(root, name), encoding='utf-8-sig') as f:
                    text = f.read()
                for match in FORGE_MENU.finditer(text):
                    args = match.group(1)
                    path = re.match(r'\s*' + STRINGS, args)
                    named = {key: value for key, value in re.findall(r'(\w+)\s*=\s*(' + STRINGS + ')', args)}
                    menus[match.group(2)] = (literal(path.group(0)) if path else None,
                                             literal(named['Name']) if 'Name' in named else None,
                                             literal(named['Description']) if 'Description' in named else None)
    return menus


def literal(text):
    """A C# string's value: its literals joined, as "a" + "b", each unescaped."""
    return ''.join(re.sub(r'\\(.)', r'\1', part) for part in re.findall(STRING, text))


def catalog_section(guide, families, types, menus):
    """The guide's page of built-in kinds, as a section after its own."""
    roots = {name: 'FarEmerald.PlayForge.' + name for _, name in families}
    tables = collections.OrderedDict((title, []) for title, _ in families)
    for item in types:
        if item.kind != 'Class' or re.search(r'\b(abstract|static)\b', item.declaration):
            continue
        best = None
        for title, name in families:
            root = roots[name]
            if root in item.bases:
                nearness = item.bases.index(root)
            elif root in item.interfaces:
                nearness = -1
            else:
                continue
            if best is None or nearness > best[0]:
                best = (nearness, title)
        if best:
            tables[best[1]].append(item)
    lines = ['The kinds PlayForge ships for this guide\'s pickers, each with its menu and what it does, as its picker',
             'says. A project\'s own kinds join them in the same pickers.', '']
    count = 0
    for title, kinds in tables.items():
        if not kinds:
            continue
        lines += ['### %s' % title, '', '| Kind | Menu | What it does |', '|---|---|---|']
        for item in sorted(kinds, key=lambda i: i.name.lower()):
            simple = item.name.split('.')[-1]
            path, shown, description = menus.get(re.sub(r'<.*$', '', simple), (None, None, None))
            if path is None:
                for base in reversed(item.bases):
                    inherited = menus.get(re.sub(r'`.*$', '', base.split('.')[-1]))
                    if inherited and inherited[0] is not None:
                        path = inherited[0]
                        break
            text = description or first_of(item.summary)
            kind = '`%s`' % item.name + (' (**%s**)' % shown if shown else '')
            text = text.replace('|', '\\|').replace('<', '&lt;').replace('*', '\\*')
            lines.append('| %s | %s | %s |' % (kind, path.replace('/', ' › ') if path else '—', text))
            count += 1
        lines.append('')
    section = Section(CATALOG_TITLE, lines)
    section.slug = slug(CATALOG_TITLE)
    section.page_anchors = Anchors()
    section.guide_anchor = None
    return section, count


def first_of(text):
    """A summary's first sentence."""
    match = re.search(r'^(.+?\.)(\s|$)', text or '')
    return match.group(1) if match else (text or '')


def first_sentence(lines):
    """A section's opening sentence, for its line in its guide's list: its first paragraph's text up to its first full
    stop, links as their text."""
    text = []
    for line, code in zip(lines, code_mask(lines)):
        stripped = line.strip()
        if code or stripped.startswith(('|', '#', '>', '<')):
            if text:
                break
            continue
        if not stripped:
            if text:
                break
            continue
        text.append(re.sub(r'^([-*]|\d+\.)\s+', '', stripped))
    sentence = LINK.sub(lambda m: m.group(1), ' '.join(text))
    match = re.search(r'^(.+?\.)(\s|$)', sentence)
    sentence = match.group(1) if match else sentence
    if len(sentence) > 220:
        sentence = sentence[:220].rsplit(' ', 1)[0].rstrip(',;:') + '…'
        if sentence.count('`') % 2:
            sentence = sentence[:sentence.rfind('`')].rstrip() + '…'
        if sentence.count('**') % 2:
            sentence = sentence[:sentence.rfind('**')] + sentence[sentence.rfind('**') + 2:]
    return sentence.rstrip(':;')


def demote(lines):
    """A section's lines on its own page: each heading one level up, as its ## heading is the page's title."""
    out = []
    for line, code in zip(lines, code_mask(lines)):
        heading = None if code else HEADING.match(line)
        out.append(line[1:] if heading and len(heading.group(1)) > 2 else line)
    return out


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with io.open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)


def front(uid, title=None):
    return '---\nuid: %s\n%s---\n\n' % (uid, 'title: %s\n' % title if title else '')


def build_guides(names, types):
    keys = [key for _, group in GUIDE_GROUPS for key in group]
    shipped = sorted(name[:-3] for name in os.listdir(DOCS) if name.endswith('.md'))
    for key in shipped:
        if key not in keys:
            problem('%s.md is no guide on the site: list it in GUIDE_GROUPS in prepare.py' % key)
    for key in keys:
        if key not in shipped:
            problem('GUIDE_GROUPS lists %s, which Documentation lacks' % key)
    guides = {key: Guide(key) for key in keys if key in shipped}
    menus = forge_menus()
    kinds = 0
    for key, families in CATALOGS:
        guide = guides.get(key)
        if guide is None:
            continue
        if any(section.slug == slug(CATALOG_TITLE) for section in guide.sections):
            raise SystemExit('%s.md has a section the catalog page would be' % key)
        section, count = catalog_section(guide, families, types, menus)
        guide.sections.append(section)
        kinds += count
    print('Catalogs: %d built-in kinds' % kinds)
    shutil.rmtree(GUIDES, ignore_errors=True)
    named_in = collections.defaultdict(list)   # each type's sections, with how often each names it
    area_types = collections.defaultdict(list)
    for item in types:
        if item.area:
            area_types[item.area[2]].append(item)

    for guide in guides.values():
        folder = os.path.join(GUIDES, guide.slug)
        pages = [(None, guide.intro)] + [(section, section.lines) for section in guide.sections]
        for n, (section, lines) in enumerate(pages):
            text = rewrite_links(guide, '\n'.join(demote(lines)).strip('\n'), guides)
            linked = set()
            body, mentioned = link_types(text.split('\n'), names, linked)
            for uid, count in mentioned.items():
                named_in[uid].append((count, guide, section))
            title = guide.title if section is None else plain(section.title)
            page = [front(guide.uid(section)), '# %s\n\n' % (guide.title if section is None else section.title)]
            page.append('\n'.join(body).strip('\n') + '\n')
            if section is None:
                if guide.sections:
                    page.append('\n## In this guide\n\n')
                    for each in guide.sections:
                        line = first_sentence(rewrite_links(guide, '\n'.join(each.lines), guides).split('\n'))
                        page.append('- [%s](%s.md)%s\n' % (each.title, each.slug, ': ' + line if line else ''))
                own = sorted({item.uid: item for item in area_types[guide.key]}.values(), key=lambda i: i.name.lower())
                if own:
                    page.append('\n## In the API\n\nThe types of this guide\'s area: %s.\n' % ', '.join(
                        '[%s](%s)' % (item.name.replace('<', '&lt;'), xref(item.uid)) for item in own))
            else:
                before = pages[n - 1][0]
                after = pages[n + 1][0] if n + 1 < len(pages) else None
                steps = ['[← %s](%s)' % (plain(before.title), before.slug + '.md') if before else
                         '[← %s](index.md)' % guide.name,
                         '[%s](index.md)' % guide.name]
                if after:
                    steps.append('[%s →](%s.md)' % (plain(after.title), after.slug))
                page.append('\n---\n\n' + ' · '.join(steps) + '\n')
            write(os.path.join(folder, (section.slug if section else 'index') + '.md'), ''.join(page))

    toc = []
    for group, group_keys in GUIDE_GROUPS:
        entries = []
        for key in group_keys:
            guide = guides.get(key)
            if guide is None:
                continue
            entry = '- name: %s\n  href: %s/index.md\n' % (yaml_text(guide.name), guide.slug)
            if guide.sections:
                entry += '  items:\n' + ''.join('  - name: %s\n    href: %s/%s.md\n' % (
                    yaml_text(plain(section.title)), guide.slug, section.slug) for section in guide.sections)
            entries.append(entry)
        if len(group_keys) == 1:
            toc.extend(entries)
        else:
            toc.append('- name: %s\n  items:\n' % yaml_text(group) + ''.join(
                ''.join('  ' + line + '\n' for line in entry.rstrip('\n').split('\n')) for entry in entries))
    write(os.path.join(GUIDES, 'toc.yml'), ''.join(toc))
    return guides, named_in


def yaml_text(text):
    return "'" + text.replace("'", "''") + "'"


# --------------------------------------------------------------------------------------------------------------- API

def build_api(guides, named_in, types):
    shutil.rmtree(APIDOC, ignore_errors=True)
    os.makedirs(APIDOC)
    for item in types:
        sections = sorted(named_in.get(item.uid, []), key=lambda e: (-e[0], list(guides).index(e[1].key),
                                                                     e[1].sections.index(e[2]) if e[2] else -1))
        lines = []
        guide = guides.get(item.area[2]) if item.area else None
        if guide is not None:
            lines.append('**Guide:** [%s](%s)' % (guide.name, xref(guide.uid())))
        links = ['[%s](%s)' % (each.name + (': ' + plain(section.title) if section else ''), xref(each.uid(section)))
                 for _, each, section in sections[:MOST_SECTIONS]]
        if links:
            more = len(sections) - MOST_SECTIONS
            lines.append('**Named in:** ' + ' · '.join(links) + (
                ', and %d more section%s' % (more, '' if more == 1 else 's') if more > 0 else ''))
        if not lines:
            continue
        text = '\n\n'.join(lines)
        name = re.sub(r'[^A-Za-z0-9._-]', '_', item.uid)
        write(os.path.join(APIDOC, name + '.md'), '---\nuid: %s\nconceptual: *content\n---\n\n%s\n' % (
            item.uid, text))

    by_area = collections.OrderedDict()
    def position(key):
        order = AREA_ORDER.get(key[0], [])
        return PARTS.index(key[0]), order.index(key[1]) if key[1] in order else len(order), key[1]

    ordered = sorted({(part, area) for _, part, area, _ in AREAS}, key=position)
    for key in ordered:
        by_area.setdefault(key, [])
    for item in types:
        if item.area:
            by_area[item.area[:2]].append(item)
    toc = ['### YamlMime:TableOfContent\nitems:\n- name: Overview\n  href: index.md\n']
    index = []
    with io.open(os.path.join(HERE, 'api.md'), encoding='utf-8') as f:
        index.append(f.read().rstrip('\n') + '\n')
    for part in PARTS:
        areas = [(area, sorted(items, key=lambda i: i.name.lower())) for (p, area), items in by_area.items()
                 if p == part and items]
        if not areas:
            continue
        toc.append('- name: %s\n  items:\n' % yaml_text(part))
        index.append('\n## %s\n' % part)
        # A part of one area of its own name, as the test kit, lists its types itself
        alone = len(areas) == 1 and areas[0][0] == part
        for area, items in areas:
            indent = '  ' if alone else '    '
            if not alone:
                toc.append('  - name: %s\n    items:\n' % yaml_text(area))
            toc.extend('%s- uid: %s\n%s  name: %s\n' % (indent, yaml_text(i.uid), indent, yaml_text(i.name))
                       for i in items)
            guide_keys = []
            for i in items:
                if i.area[2] not in guide_keys:
                    guide_keys.append(i.area[2])
            covered = [guides[k] for k in guide_keys if k in guides]
            index.append('\n' if alone else '\n### %s\n\n' % area)
            if covered:
                index.append('In the guides: %s.\n\n' % ', '.join(
                    '[%s](%s)' % (g.name, xref(g.uid())) for g in covered))
            index.append(', '.join('[%s](%s)' % (i.name.replace('<', '&lt;'), xref(i.uid)) for i in items) + '\n')
    namespaces = sorted(uid for uid, item in API_ITEMS.items() if item.kind == 'Namespace')
    toc.append('- name: Namespaces\n  items:\n')
    toc.extend('  - uid: %s\n    name: %s\n' % (yaml_text(uid), yaml_text(uid)) for uid in namespaces)
    write(os.path.join(API, 'toc.yml'), ''.join(toc))
    write(os.path.join(API, 'index.md'), ''.join(index))


API_ITEMS = {}

UNITY_XREFMAP = os.path.join(HERE, 'obj', 'unity.xrefmap.yml')
UNITY_DOCS = 'https://docs.unity3d.com/ScriptReference/'
EXTERNAL = re.compile(r'^- uid: (Unity(?:Engine|Editor)\.\S+)\n  commentId: ([TPFME]):(\S+)\n', re.M)


def unity_xrefmap():
    """An xref map giving each of Unity's types and members the API pages name its page in Unity's Scripting Reference:
    a type at its name, its namespace after UnityEngine or UnityEditor before it; a property or field at Type-name; a
    method at Type.Name."""
    refs = {}
    for name in os.listdir(RAW_API):
        if not name.endswith('.yml'):
            continue
        with io.open(os.path.join(RAW_API, name), encoding='utf-8') as f:
            text = f.read()
        for uid, kind, comment in EXTERNAL.findall(text):
            if uid in refs:
                continue
            path = re.sub(r'\(.*$', '', comment).split('.', 1)[1].replace('`', '_').replace('{', '_').replace('}', '')
            if kind == 'T':
                page = path
            else:
                owner, _, member = path.rpartition('.')
                if member == '#ctor':
                    page = owner + '-ctor'
                elif kind == 'M':
                    page = owner + '.' + member
                else:
                    page = owner + '-' + member
            refs[uid] = page
    lines = ['### YamlMime:XRefMap\nsorted: true\nreferences:\n']
    for uid in sorted(refs):
        lines.append('- uid: %s\n  name: %s\n  href: %s%s.html\n' % (
            yaml_text(uid), yaml_text(uid.rsplit('.', 1)[-1]), UNITY_DOCS, refs[uid]))
    write(UNITY_XREFMAP, ''.join(lines))
    return len(refs)

FORGE_DOCS = os.path.join(REPO, 'Assets', 'PlayForge', 'Editor', 'Util', 'ForgeDocs.cs')
TOPIC = re.compile(r'\["([^"]+)"\]\s*=\s*\("([A-Z_]+)\.md",\s*(null|"([^"]*)")\)')


def check_help_topics(guides):
    """Each help (?) button's topic, in ForgeDocs.cs, is a page here: its section's, as ForgeDocs.UrlOf names it."""
    with io.open(FORGE_DOCS, encoding='utf-8-sig') as f:
        topics = TOPIC.findall(f.read())
    if not topics:
        problem('ForgeDocs.cs names no help topics that prepare.py can read')
    for topic, page, _, heading in topics:
        guide = guides.get(page)
        if guide is None:
            problem('Help topic %s names %s.md, which is no guide here' % (topic, page))
        elif heading and slug(heading) not in {section.slug for section in guide.sections}:
            problem('Help topic %s names "%s", which %s.md has no ## section of' % (topic, heading, page))
    return len(topics)


def main():
    global API_ITEMS
    if not os.path.isdir(RAW_API):
        raise SystemExit('No obj/api/: run DocFX\'s metadata step first, as build.py does')
    API_ITEMS, types = read_api()
    names = Names(API_ITEMS, types)
    guides, named_in = build_guides(names, types)
    build_api(guides, named_in, types)
    unity_xrefmap()
    topics = check_help_topics(guides)
    linked_types = sum(1 for item in types if named_in.get(item.uid))
    print('Guides: %d, as %d pages. API: %d types, %d of them named in the guides. Help topics: %d.' % (
        len(guides), sum(1 + len(g.sections) for g in guides.values()), len(types), linked_types, topics))
    if problems:
        print('\n'.join(problems), file=sys.stderr)
        raise SystemExit(1)


if __name__ == '__main__':
    main()
