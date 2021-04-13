# CMOD binary format

All values are stored in little-endian format.

## Enumerations

Enumerations are stored as signed 16-bit integers.

### Tokens

Values are defined in the Celestia codebase in modelfile.h. The mappings to the equivalent tokens
in the CMOD ASCII format are as follows:

| Numeric value | ASCII equivalent |
|---------------|------------------|
| 1001          | `material `      |
| 1002          | `end_material`   |
| 1003          | `diffuse`        |
| 1004          | `specular`       |
| 1005          | `specpower`      |
| 1006          | `opacity`        |
| 1007          | `texture`        |
| 1009          | `mesh`           |
| 1010          | `end_mesh`       |
| 1011          | `vertexdesc`     |
| 1012          | `end_vertexdesc` |
| 1013          | `vertices`       |
| 1014          | `emissive`       |
| 1015          | `blend`          |

### Texture semantics

These correspond to the numbers after the CMOD texture token (e.g. texture0 corrseponds to the
texture token 1007 followed by texture semantic 0). These have the following interpretations,
defined in material.h in the Celestia codebase.

| Numeric value | Semantic |
|---------------|----------|
| 0             | diffuse  |
| 1             | normal   |
| 2             | specular |
| 3             | emissive |

### Data types

Values in the material definition sections are encoded with a corresponding data type. These are
defined in the file modelfile.h in the Celestia codebase.

| Numeric value | Data type | Value stored as                                                         |
|---------------|-----------|-------------------------------------------------------------------------|
| 1             | float1    | 32-bit floating point                                                   |
| 2             | float2    | 2×32-bit floating point                                                 |
| 3             | float3    | 3×32-bit floating point                                                 |
| 4             | float4    | 4×32-bit floating point                                                 |
| 5             | string    | 16-bit unsigned integer length *L*, followed by *L* ASCII encoded bytes |
| 6             | uint32    | 32-bit unsigned integer                                                 |
| 7             | color     | 3×32-bit floating point in the order red, green, blue                   |

In practice, only data types float1, float3 and string are used.

### Blend modes

Blend modes are defined in the file material.h in the Celestia codebase. The corresponding keywords
in the CMOD ASCII format are as follows:

| Numeric value | ASCII equivalent |
|---------------|------------------|
| 0             | `normal`         |
| 1             | `add`            |
| 2             | `premultiplied`  |

### Vertex attribute semantics

Vertex attribute semantics are defined in the file mesh.h in the Celestia codebase. The
corresponding keywords in the CMOD ASCII format are as follows:

| Numeric value | ASCII equivalent |
|---------------|------------------|
| 0             | `position`       |
| 1             | `color0`         |
| 2             | `color1`         |
| 3             | `normal`         |
| 4             | `tangent`        |
| 5             | `texcoord0`      |
| 6             | `texcoord1`      |
| 7             | `texcoord2`      |
| 8             | `texcoord3`      |
| 9             | `pointsize`      |
| 10            | (nextposition)   |
| 11            | (scalefactor)    |

The nextposition and scalefactor attribute semantics have no equivalent in the ASCII format, possibly
these are unused?

### Vertex attribute formats

Vertex attribute formats are defined in mesh.h in the Celestia codebase. The corresponding formats
in the CMOD ASCII format are as follows:

| Numeric value | ASCII equivalent | Value stored as         |
|---------------|------------------|-------------------------|
| 0             | `f1`             | 32-bit floating point   |
| 1             | `f2`             | 2×32-bit floating point |
| 2             | `f3`             | 3×32-bit floating point |
| 3             | `f4`             | 4×32-bit floating point |
| 4             | `ub4`            | 4×unsigned byte         |

### Primitive types

Primitive types are defined in mesh.h in the Celestia codebase. The equivalents to the CMOD ASCII
keywords are as follows:

| Numeric value | ASCII equivalent |
|---------------|------------------|
| 0             | `trilist`        |
| 1             | `tristrip`       |
| 2             | `trifan`         |
| 3             | `linelist`       |
| 4             | `linestrip`      |
| 5             | `points`         |
| 6             | `sprites`        |

## File structure

- 16-byte header, ASCII encoded string `#celmodel_binary`
- One or more materials (see below)
- One or more meshes ( see below)

### Materials

- Token `material` (1001)
- One or more material entries, given by a token followed by a data type, followed by the value
  encoded as specified in the Data Types section above.

  | Token                              | Data type          |
  |------------------------------------|--------------------|
  | `diffuse` (1003)                   | color (7)          |
  | `specular` (1004)                  | color (7)          |
  | `specpower` (1005)                 | float1 (1)         |
  | `opacity` (1006)                   | float1 (1)         |
  | `texture` (1007), texture semantic | string (5)         |
  | `emissive` (1014)                  | color (7)          |
  | `blend` (1015)                     | (none), blend mode |

  Note that the `blend` token (1015) is immediately followed by the blend mode enumeration value
  (see above).
- Token `end_material` (1002)

### Meshes

- Token `mesh` (1009)
- Vertex description:
  - Token `vertexdesc` (1011)
  - One or more vertex description entries, consisting of a vertex attribute semantic (see above)
    followed by a vertex attribute format (see above).
  - Token `end_vertexdesc` (1012)
- Vertex data:
  - Token `vertices` (1013)
  - Unsigned 32-bit integer vertex count *V*
  - *V* entries of vertex data in the format specified by the vertex description entries.
- One or more primitives:
  - Primitive type (see above)
  - 32-bit unsigned integer material index (0-based)
  - 32-bit unsigned integer index count *I*
  - *I*×32-bit unsigned integers indexing the vertices (0-based)
- Token `end_mesh` (1010)
