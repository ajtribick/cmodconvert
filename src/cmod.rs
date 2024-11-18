use std::{
    fmt::{self, Display},
    io,
    str::FromStr,
};

use num_derive::FromPrimitive;
use thiserror::Error;

#[derive(Default)]
pub struct CmodData {
    pub materials: Vec<Material>,
    pub meshes: Vec<Mesh>,
}

#[derive(Default, Clone)]
pub struct Material {
    pub diffuse: Option<Color>,
    pub specular: Option<Color>,
    pub emissive: Option<Color>,
    pub specular_power: Option<f32>,
    pub opacity: Option<f32>,
    pub blend_mode: Option<BlendMode>,
    pub textures: [Option<Box<str>>; 4],
}

impl Material {
    pub const DIFFUSE: usize = 0;
    pub const SPECULAR: usize = 2;
    pub const EMISSIVE: usize = 3;
}

pub struct Mesh {
    pub attributes: Vec<VertexAttribute>,
    pub primitives: Vec<Primitive>,
    pub vertex_count: usize,
}

pub struct VertexDescriptor {
    pub attribute_type: AttributeType,
    pub format: AttributeFormat,
}

pub struct VertexAttribute {
    pub attribute_type: AttributeType,
    pub data: AttributeData,
}

impl VertexAttribute {
    pub fn new(descriptor: &VertexDescriptor, capacity: usize) -> Self {
        Self {
            attribute_type: descriptor.attribute_type,
            data: AttributeData::new(descriptor.format, capacity),
        }
    }
}

pub enum AttributeData {
    Float1(Vec<f32>),
    Float2(Vec<[f32; 2]>),
    Float3(Vec<[f32; 3]>),
    Float4(Vec<[f32; 4]>),
    UByte4(Vec<[u8; 4]>),
}

impl AttributeData {
    pub fn new(format: AttributeFormat, capacity: usize) -> Self {
        match format {
            AttributeFormat::Float1 => Self::Float1(Vec::with_capacity(capacity)),
            AttributeFormat::Float2 => Self::Float2(Vec::with_capacity(capacity)),
            AttributeFormat::Float3 => Self::Float3(Vec::with_capacity(capacity)),
            AttributeFormat::Float4 => Self::Float4(Vec::with_capacity(capacity)),
            AttributeFormat::UByte4 => Self::UByte4(Vec::with_capacity(capacity)),
        }
    }
}

pub struct Primitive {
    pub primitive_type: PrimitiveType,
    pub material_index: u32,
    pub indices: Vec<u32>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, FromPrimitive)]
#[repr(u16)]
pub enum Token {
    Material = 1001,
    EndMaterial = 1002,
    Diffuse = 1003,
    Specular = 1004,
    SpecularPower = 1005,
    Opacity = 1006,
    Texture = 1007,
    Mesh = 1009,
    EndMesh = 1010,
    VertexDesc = 1011,
    EndVertexDesc = 1012,
    Vertices = 1013,
    Emissive = 1014,
    Blend = 1015,
}

impl Token {
    const MATERIAL: &str = "material";
    const END_MATERIAL: &str = "end_material";
    const DIFFUSE: &str = "diffuse";
    const SPECULAR: &str = "specular";
    const SPECPOWER: &str = "specpower";
    const OPACITY: &str = "opacity";
    const TEXTURE: &str = "texture";
    const MESH: &str = "mesh";
    pub(crate) const END_MESH: &str = "end_mesh";
    const VERTEXDESC: &str = "vertexdesc";
    pub(crate) const END_VERTEXDESC: &str = "end_vertexdesc";
    const VERTICES: &str = "vertices";
    const EMISSIVE: &str = "emissive";
    const BLEND: &str = "blend";
}

impl Display for Token {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Material => f.write_str(Self::MATERIAL),
            Self::EndMaterial => f.write_str(Self::END_MATERIAL),
            Self::Diffuse => f.write_str(Self::DIFFUSE),
            Self::Specular => f.write_str(Self::SPECULAR),
            Self::SpecularPower => f.write_str(Self::SPECPOWER),
            Self::Opacity => f.write_str(Self::OPACITY),
            Self::Texture => f.write_str(Self::TEXTURE),
            Self::Mesh => f.write_str(Self::MESH),
            Self::EndMesh => f.write_str(Self::END_MESH),
            Self::VertexDesc => f.write_str(Self::VERTEXDESC),
            Self::EndVertexDesc => f.write_str(Self::END_VERTEXDESC),
            Self::Vertices => f.write_str(Self::VERTICES),
            Self::Emissive => f.write_str(Self::EMISSIVE),
            Self::Blend => f.write_str(Self::BLEND),
        }
    }
}

impl FromStr for Token {
    type Err = CmodError;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            Self::MATERIAL => Ok(Self::Material),
            Self::END_MATERIAL => Ok(Self::EndMaterial),
            Self::DIFFUSE => Ok(Self::Diffuse),
            Self::SPECULAR => Ok(Self::Specular),
            Self::SPECPOWER => Ok(Self::SpecularPower),
            Self::OPACITY => Ok(Self::Opacity),
            Self::TEXTURE => Ok(Self::Texture),
            Self::MESH => Ok(Self::Mesh),
            Self::END_MESH => Ok(Self::EndMesh),
            Self::VERTEXDESC => Ok(Self::VertexDesc),
            Self::END_VERTEXDESC => Ok(Self::EndVertexDesc),
            Self::VERTICES => Ok(Self::Vertices),
            Self::EMISSIVE => Ok(Self::Emissive),
            Self::BLEND => Ok(Self::Blend),
            _ => Err(CmodError::InvalidTokenName(s.into())),
        }
    }
}

#[derive(Debug)]
enum TokenOrEof {
    Token(Token),
    Eof,
}

impl From<&Option<Token>> for TokenOrEof {
    fn from(value: &Option<Token>) -> Self {
        match value {
            Some(token) => Self::Token(*token),
            None => Self::Eof,
        }
    }
}

impl Display for TokenOrEof {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Token(token) => token.fmt(f),
            Self::Eof => f.write_str("#EOF#"),
        }
    }
}

#[derive(Debug, PartialEq, Eq, FromPrimitive)]
#[repr(u16)]
pub enum DataType {
    Float1 = 1,
    Float2 = 2,
    Float3 = 3,
    Float4 = 4,
    String = 5,
    UInt32 = 6,
    Color = 7,
}

impl Display for DataType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Float1 => f.write_str("float1"),
            Self::Float2 => f.write_str("float2"),
            Self::Float3 => f.write_str("float3"),
            Self::Float4 => f.write_str("float4"),
            Self::String => f.write_str("string"),
            Self::UInt32 => f.write_str("uint32"),
            Self::Color => f.write_str("color"),
        }
    }
}

#[derive(Debug, Clone, Copy, FromPrimitive)]
pub enum BlendMode {
    Normal = 0,
    Additive = 1,
    PremultipliedAlpha = 2,
}

impl BlendMode {
    const NORMAL: &str = "normal";
    const ADD: &str = "add";
    const PREMULTIPLIED: &str = "premultiplied";
}

impl Display for BlendMode {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Normal => f.write_str(Self::NORMAL),
            Self::Additive => f.write_str(Self::ADD),
            Self::PremultipliedAlpha => f.write_str(Self::PREMULTIPLIED),
        }
    }
}

impl FromStr for BlendMode {
    type Err = CmodError;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            Self::NORMAL => Ok(Self::Normal),
            Self::ADD => Ok(Self::Additive),
            Self::PREMULTIPLIED => Ok(Self::PremultipliedAlpha),
            _ => Err(CmodError::InvalidBlendModeName(s.into())),
        }
    }
}

#[derive(Debug, Clone, Copy)]
pub struct Color {
    pub red: f32,
    pub green: f32,
    pub blue: f32,
}

impl Display for Color {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{} {} {}", self.red, self.green, self.blue)
    }
}

#[derive(Clone, Copy, FromPrimitive)]
pub enum TextureSemantic {
    Diffuse = 0,
    Normal = 1,
    Specular = 2,
    Emissive = 3,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, FromPrimitive)]
#[repr(u16)]
pub enum AttributeType {
    Position = 0,
    Color0 = 1,
    Color1 = 2,
    Normal = 3,
    Tangent = 4,
    TexCoord0 = 5,
    TexCoord1 = 6,
    TexCoord2 = 7,
    TexCoord3 = 8,
    PointSize = 9,
    NextPosition = 10,
    ScaleFactor = 11,
}

impl AttributeType {
    const POSITION: &str = "position";
    const COLOR0: &str = "color0";
    const COLOR1: &str = "color1";
    const NORMAL: &str = "normal";
    const TANGENT: &str = "tangent";
    const TEXCOORD0: &str = "texcoord0";
    const TEXCOORD1: &str = "texcoord1";
    const TEXCOORD2: &str = "texcoord2";
    const TEXCOORD3: &str = "texcoord3";
    const POINTSIZE: &str = "pointsize";
    // nextposition and scalefactor have no equivalent in ASCII CMOD files
    const NEXTPOSITION: &str = "#nextposition#";
    const SCALEFACTOR: &str = "#scalefactor#";

    pub fn is_valid_format(&self, format: AttributeFormat) -> bool {
        match self {
            Self::Position => {
                format == AttributeFormat::Float3 || format == AttributeFormat::Float4
            }
            Self::Normal => format == AttributeFormat::Float3,
            Self::TexCoord0 => {
                format == AttributeFormat::Float1
                    || format == AttributeFormat::Float2
                    || format == AttributeFormat::Float3
            }
            _ => true, // don't care about other attributes
        }
    }
}

impl Display for AttributeType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Position => f.write_str(Self::POSITION),
            Self::Color0 => f.write_str(Self::COLOR0),
            Self::Color1 => f.write_str(Self::COLOR1),
            Self::Normal => f.write_str(Self::NORMAL),
            Self::Tangent => f.write_str(Self::TANGENT),
            Self::TexCoord0 => f.write_str(Self::TEXCOORD0),
            Self::TexCoord1 => f.write_str(Self::TEXCOORD1),
            Self::TexCoord2 => f.write_str(Self::TEXCOORD2),
            Self::TexCoord3 => f.write_str(Self::TEXCOORD3),
            Self::PointSize => f.write_str(Self::POINTSIZE),
            Self::NextPosition => f.write_str(Self::NEXTPOSITION),
            Self::ScaleFactor => f.write_str(Self::SCALEFACTOR),
        }
    }
}

impl FromStr for AttributeType {
    type Err = CmodError;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            Self::POSITION => Ok(Self::Position),
            Self::COLOR0 => Ok(Self::Color0),
            Self::COLOR1 => Ok(Self::Color1),
            Self::NORMAL => Ok(Self::Normal),
            Self::TANGENT => Ok(Self::Tangent),
            Self::TEXCOORD0 => Ok(Self::TexCoord0),
            Self::TEXCOORD1 => Ok(Self::TexCoord1),
            Self::TEXCOORD2 => Ok(Self::TexCoord2),
            Self::TEXCOORD3 => Ok(Self::TexCoord3),
            Self::POINTSIZE => Ok(Self::PointSize),
            _ => Err(CmodError::InvalidAttributeTypeName(s.into())),
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, FromPrimitive)]
pub enum AttributeFormat {
    Float1 = 0,
    Float2 = 1,
    Float3 = 2,
    Float4 = 3,
    UByte4 = 4,
}

impl AttributeFormat {
    const F1: &str = "f1";
    const F2: &str = "f2";
    const F3: &str = "f3";
    const F4: &str = "f4";
    const UB4: &str = "ub4";
}

impl Display for AttributeFormat {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Float1 => f.write_str(Self::F1),
            Self::Float2 => f.write_str(Self::F2),
            Self::Float3 => f.write_str(Self::F3),
            Self::Float4 => f.write_str(Self::F4),
            Self::UByte4 => f.write_str(Self::UB4),
        }
    }
}

impl FromStr for AttributeFormat {
    type Err = CmodError;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            Self::F1 => Ok(Self::Float1),
            Self::F2 => Ok(Self::Float2),
            Self::F3 => Ok(Self::Float3),
            Self::F4 => Ok(Self::Float4),
            Self::UB4 => Ok(Self::UByte4),
            _ => Err(CmodError::InvalidAttributeFormatName(s.into())),
        }
    }
}

#[derive(FromPrimitive)]
pub enum PrimitiveType {
    TriList = 0,
    TriStrip = 1,
    TriFan = 2,
    LineList = 3,
    LineStrip = 4,
    PointList = 5,
    SpriteList = 6,
}

impl PrimitiveType {
    const TRILIST: &str = "trilist";
    const TRISTRIP: &str = "tristrip";
    const TRIFAN: &str = "trifan";
    const LINELIST: &str = "linelist";
    const LINESTRIP: &str = "linestrip";
    const POINTS: &str = "points";
    const SPRITES: &str = "sprites";

    pub fn min_points(&self) -> usize {
        match self {
            Self::TriList => 3,
            Self::TriStrip => 3,
            Self::TriFan => 3,
            Self::LineList => 2,
            Self::LineStrip => 2,
            Self::PointList => 1,
            Self::SpriteList => 1,
        }
    }
}

impl Display for PrimitiveType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::TriList => f.write_str(Self::TRILIST),
            Self::TriStrip => f.write_str(Self::TRISTRIP),
            Self::TriFan => f.write_str(Self::TRIFAN),
            Self::LineList => f.write_str(Self::LINELIST),
            Self::LineStrip => f.write_str(Self::LINESTRIP),
            Self::PointList => f.write_str(Self::POINTS),
            Self::SpriteList => f.write_str(Self::SPRITES),
        }
    }
}

impl FromStr for PrimitiveType {
    type Err = CmodError;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            Self::TRILIST => Ok(Self::TriList),
            Self::TRISTRIP => Ok(Self::TriStrip),
            Self::TRIFAN => Ok(Self::TriFan),
            Self::LINELIST => Ok(Self::LineList),
            Self::LINESTRIP => Ok(Self::LineStrip),
            Self::POINTS => Ok(Self::PointList),
            Self::SPRITES => Ok(Self::SpriteList),
            _ => Err(CmodError::InvalidPrimitiveTypeName(s.into())),
        }
    }
}

#[derive(Debug, Error)]
#[non_exhaustive]
pub enum CmodError {
    #[error("IO error {0}")]
    IOError(#[from] io::Error),
    #[error("bad signature")]
    BadSignature,
    #[error("invalid token {0}")]
    InvalidToken(u16),
    #[error("unexpected token {}", TokenOrEof::from(.0))]
    UnexpectedToken(Option<Token>),
    #[error("invalid data type {0}")]
    InvalidDataType(u16),
    #[error("unexpected data type {0}")]
    UnexpectedDataType(DataType),
    #[error("invalid blend mode {0}")]
    InvalidBlendMode(u16),
    #[error("invalid texture semantic {0}")]
    InvalidTextureSemantic(u16),
    #[error("invalid attribute type {0}")]
    InvalidAttributeType(u16),
    #[error("duplicate attribute type")]
    DuplicateAttributeType,
    #[error("invalid attribute format {0}")]
    InvalidAttributeFormat(u16),
    #[error("unexpected attribute format {0}")]
    UnexpectedAttributeFormat(AttributeFormat),
    #[error("invalid material count")]
    InvalidMaterialCount,
    #[error("invalid vertex count")]
    InvalidVertexCount,
    #[error("invalid primitive type {0}")]
    InvalidPrimitiveType(u16),
    #[error("invalid material index {0}")]
    InvalidMaterial(u32),
    #[error("invalid index count")]
    InvalidIndexCount,
    #[error("invalid vertex index {0}")]
    InvalidIndex(u32),
    #[error("invalid UTF-8 data")]
    InvalidUTF8,
    #[error("unexpected char")]
    UnexpectedChar,
    #[error("unclosed string")]
    UnclosedString,
    #[error("bad number format")]
    BadNumber,
    #[error("expected name token")]
    NotAName,
    #[error("expected integer token")]
    NotAnInteger,
    #[error("expected float token")]
    NotAFloat,
    #[error("expected quoted string")]
    NotAString,
    #[error("expected byte value")]
    NotAByte,
    #[error("invalid token name {0}")]
    InvalidTokenName(Box<str>),
    #[error("invalid blend mode name {0}")]
    InvalidBlendModeName(Box<str>),
    #[error("invalid attribute type name {0}")]
    InvalidAttributeTypeName(Box<str>),
    #[error("invalid attribute format name {0}")]
    InvalidAttributeFormatName(Box<str>),
    #[error("invalid primitive type name {0}")]
    InvalidPrimitiveTypeName(Box<str>),
}
