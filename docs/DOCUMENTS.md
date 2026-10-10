# Document transformations

All operations use native Projecta JSON tokens. Results are detached from inputs. Failed patches/pipelines do not mutate the original document. Limits bound definitions, operations, depth, nodes and character growth; these are cooperative controls, not process memory limits.

## JSON Pointer

`JsonPointer.Parse(pointer).Resolve(document)` supports the string form: empty pointer for root, `/` for an empty property name, `~0` for `~`, `~1` for `/`. Array indexes must use canonical nonnegative decimal notation. URI-fragment pointers are not accepted. Missing paths fail; `TryResolve` distinguishes absence from JSON null. Resolved values are detached.

## Patches

`JsonPatchDocument.Parse(json).Apply(document)` supports `add`, `remove`, `replace`, `copy`, `move` and `test`, applied sequentially to a working copy. Parsed operations/values are frozen. Object adds replace an existing member; array adds insert and `-` appends. Replace/remove require an existing path. Move cannot place a value inside its own descendant. Test compares JSON values structurally, including equivalent numeric representations.

**Document-preserving profile:** replacing/adding/copying to the root is supported; removing the root is rejected explicitly. This qualification prevents claiming complete compatibility with every external patch implementation. No patch operation automatically authorizes a path or applies changes to EF entities.

```json
[
  { "op": "test", "path": "/status", "value": "draft" },
  { "op": "replace", "path": "/status", "value": "ready" },
  { "op": "copy", "from": "/name", "path": "/displayName" }
]
```

Duplicate operation members and invalid required operands fail at parse time. Unknown extension members are ignored; unknown operation names are rejected. There is no partial commit to the original JSON tree.

## Merge patches

`JsonMergePatch.Apply(target, patch)` recursively merges object members, deletes properties whose patch value is null, and replaces non-object/array values as a whole. A null patch replaces the whole root with JSON null. A property that must retain JSON null cannot use null as a merge-patch assignment; use a regular add/replace patch instead.

## Versioned pipelines

`DocumentPipeline.Parse(json).Execute(input)` accepts a strict version-1 definition. Unknown/duplicate definition members fail. Operations use JSON Pointers:

```json
{
  "version": 1,
  "steps": [
    { "op": "copy", "from": "/name", "path": "/displayName" },
    { "op": "default", "path": "/status", "value": "new" },
    { "op": "ensureArray", "path": "/tags" },
    { "op": "omit", "path": "/temporary" },
    { "op": "project", "expression": "{'Name':displayName,'Status':status,'Tags':tags}" }
  ]
}
```

| Operation | Semantics |
|---|---|
| copy | Detached copy of an existing path |
| rename | Move with normal patch preconditions |
| default | Set only when absent; an existing null is preserved |
| omit | Remove when present; absence is a no-op |
| ensureArray | Missing becomes `[]`; a scalar/null is wrapped; an array is unchanged |
| project | Execute a compiled JSONata expression using the supplied projector/context |

Intermediate document limits are checked after every step. The result contains the final value and metadata-only stage traces (index, operation and elapsed milliseconds). Trace records do not contain before/after payloads. Trusted callbacks can have external side effects; document atomicity does **not** roll them back.
