use std::{
    fmt::{Display, Write},
    hash::Hash,
    iter,
};

use num_derive::FromPrimitive;
use rustc_hash::FxHashMap;
use smallvec::{smallvec, SmallVec};
use thiserror::Error;

use crate::cmod::{
    AttributeData, AttributeType, CmodData, Material, Mesh, Primitive, PrimitiveType,
};

#[derive(Default)]
pub struct WavefrontMesh {
    pub materials: Vec<Material>,
    pub positions: Vec<Position>,
    pub tex_coords: Vec<TexCoord>,
    pub normals: Vec<Normal>,
    pub primitive_groups: Vec<Vec<WavefrontPrimitive>>,
}

impl WavefrontMesh {
    pub fn try_new(cmod_data: &CmodData) -> Result<Self, WavefrontError> {
        let mut vertex_handler = VertexHandler::new(cmod_data.materials.len());
        for mesh in cmod_data.meshes.iter() {
            vertex_handler.process_mesh(mesh)?;
        }

        Ok(Self {
            materials: cmod_data.materials.clone(),
            positions: vertex_handler.positions,
            tex_coords: vertex_handler.tex_coords,
            normals: vertex_handler.normals,
            primitive_groups: vertex_handler.primitive_groups,
        })
    }
}

struct VertexHandler {
    positions: Vec<Position>,
    tex_coords: Vec<TexCoord>,
    normals: Vec<Normal>,
    position_lookup: FxHashMap<Position, u32>,
    tex_coord_lookup: FxHashMap<TexCoord, u32>,
    normal_lookup: FxHashMap<Normal, u32>,
    vertices: Vec<VertexInfo>,
    primitive_groups: Vec<Vec<WavefrontPrimitive>>,
}

impl VertexHandler {
    pub fn new(material_count: usize) -> Self {
        Self {
            positions: Default::default(),
            tex_coords: Default::default(),
            normals: Default::default(),
            position_lookup: Default::default(),
            tex_coord_lookup: Default::default(),
            normal_lookup: Default::default(),
            vertices: Default::default(),
            primitive_groups: iter::repeat_with(Default::default)
                .take(material_count)
                .collect(),
        }
    }

    pub fn process_mesh(&mut self, mesh: &Mesh) -> Result<(), WavefrontError> {
        self.process_vertices(mesh)?;
        self.process_primitives(&mesh.primitives);
        Ok(())
    }

    fn process_vertices(&mut self, mesh: &Mesh) -> Result<(), WavefrontError> {
        self.vertices.clear();

        let mut position_attribute = None;
        let mut tex_coord_attribute = None;
        let mut normal_attribute = None;

        mesh.attributes
            .iter()
            .for_each(|attribute| match attribute.attribute_type {
                AttributeType::Position => position_attribute = Some(attribute),
                AttributeType::TexCoord0 => tex_coord_attribute = Some(attribute),
                AttributeType::Normal => normal_attribute = Some(attribute),
                _ => {}
            });

        let position_attribute = position_attribute.ok_or(WavefrontError::NoPosition)?;

        for idx in 0..mesh.vertex_count {
            let position = match &position_attribute.data {
                AttributeData::Float3(f3) => Position::from(&f3[idx]),
                AttributeData::Float4(f4) => Position::from(&f4[idx]),
                _ => unreachable!(),
            };

            let position_idx = *self
                .position_lookup
                .entry(position)
                .or_insert_with_key(|key| {
                    let position_idx = self.positions.len();
                    self.positions.push(*key);
                    position_idx as u32
                });

            let tex_coord_idx = tex_coord_attribute.map(|attribute| {
                let tex_coord = match &attribute.data {
                    AttributeData::Float1(f1) => TexCoord::from(f1[idx]),
                    AttributeData::Float2(f2) => TexCoord::from(&f2[idx]),
                    AttributeData::Float3(f3) => TexCoord::from(&f3[idx]),
                    _ => unreachable!(),
                };

                *self
                    .tex_coord_lookup
                    .entry(tex_coord)
                    .or_insert_with_key(|key| {
                        let tex_coord_idx = self.tex_coords.len();
                        self.tex_coords.push(*key);
                        tex_coord_idx as u32
                    })
            });

            let normal_idx = normal_attribute.map(|attribute| {
                let normal = match &attribute.data {
                    AttributeData::Float3(f3) => Normal::from(&f3[idx]),
                    _ => unreachable!(),
                };

                *self.normal_lookup.entry(normal).or_insert_with_key(|key| {
                    let normal_idx = self.normals.len();
                    self.normals.push(*key);
                    normal_idx as u32
                })
            });

            self.vertices.push(VertexInfo {
                position_idx,
                tex_coord_idx,
                normal_idx,
            });
        }

        Ok(())
    }

    fn process_primitives(&mut self, primitives: &[Primitive]) {
        for primitive in primitives {
            let primitive_group = &mut self.primitive_groups[primitive.material_index as usize];
            match primitive.primitive_type {
                PrimitiveType::TriList => {
                    process_trilist(primitive_group, &self.vertices, &primitive.indices)
                }
                PrimitiveType::TriStrip => {
                    process_tristrip(primitive_group, &self.vertices, &primitive.indices)
                }
                PrimitiveType::TriFan => {
                    process_trifan(primitive_group, &self.vertices, &primitive.indices)
                }
                PrimitiveType::LineList => {
                    process_linelist(primitive_group, &self.vertices, &primitive.indices)
                }
                PrimitiveType::LineStrip => {
                    process_linestrip(primitive_group, &self.vertices, &primitive.indices)
                }
                PrimitiveType::PointList | PrimitiveType::SpriteList => {
                    process_points(primitive_group, &self.vertices, &primitive.indices)
                }
            }
        }
    }
}

fn process_trilist(
    primitive_group: &mut Vec<WavefrontPrimitive>,
    vertices: &[VertexInfo],
    indices: &[u32],
) {
    primitive_group.extend(indices.chunks_exact(3).map(|chunk| WavefrontPrimitive {
        category: PrimitiveCategory::Triangle,
        vertices: chunk.iter().map(|&i| vertices[i as usize]).collect(),
    }));
}

fn process_tristrip(
    primitive_group: &mut Vec<WavefrontPrimitive>,
    vertices: &[VertexInfo],
    indices: &[u32],
) {
    let mut iter = indices.iter().copied();
    let mut a = match iter.next() {
        Some(idx) => &vertices[idx as usize],
        None => return,
    };

    let mut b = match iter.next() {
        Some(idx) => &vertices[idx as usize],
        None => return,
    };

    for idx in iter {
        let c = &vertices[idx as usize];
        primitive_group.push(WavefrontPrimitive {
            category: PrimitiveCategory::Triangle,
            vertices: smallvec![*a, *b, *c],
        });
        a = b;
        b = c;
    }
}

fn process_trifan(
    primitive_group: &mut Vec<WavefrontPrimitive>,
    vertices: &[VertexInfo],
    indices: &[u32],
) {
    let mut iter = indices.iter().copied();
    let a = match iter.next() {
        Some(idx) => &vertices[idx as usize],
        None => return,
    };

    let mut b = match iter.next() {
        Some(idx) => &vertices[idx as usize],
        None => return,
    };

    for idx in iter {
        let c = &vertices[idx as usize];
        primitive_group.push(WavefrontPrimitive {
            category: PrimitiveCategory::Triangle,
            vertices: smallvec![*a, *b, *c],
        });
        b = c;
    }
}

fn process_linelist(
    primitive_group: &mut Vec<WavefrontPrimitive>,
    vertices: &[VertexInfo],
    indices: &[u32],
) {
    primitive_group.extend(indices.chunks_exact(2).map(|chunk| WavefrontPrimitive {
        category: PrimitiveCategory::Line,
        vertices: chunk.iter().map(|&i| vertices[i as usize]).collect(),
    }));
}

fn process_linestrip(
    primitive_group: &mut Vec<WavefrontPrimitive>,
    vertices: &[VertexInfo],
    indices: &[u32],
) {
    primitive_group.push(WavefrontPrimitive {
        category: PrimitiveCategory::Line,
        vertices: indices.iter().map(|&i| vertices[i as usize]).collect(),
    });
}

fn process_points(
    primitive_group: &mut Vec<WavefrontPrimitive>,
    vertices: &[VertexInfo],
    indices: &[u32],
) {
    primitive_group.push(WavefrontPrimitive {
        category: PrimitiveCategory::Point,
        vertices: indices.iter().map(|&i| vertices[i as usize]).collect(),
    });
}

#[repr(transparent)]
#[derive(Clone, Copy)]
pub struct EquatableF32(f32);

impl PartialEq for EquatableF32 {
    fn eq(&self, other: &Self) -> bool {
        self.0 == other.0 || self.0.is_nan() || other.0.is_nan()
    }
}

impl Eq for EquatableF32 {}

impl Hash for EquatableF32 {
    fn hash<H: std::hash::Hasher>(&self, state: &mut H) {
        if self.0.is_nan() {
            f32::NAN.to_ne_bytes().hash(state);
        } else {
            self.0.to_ne_bytes().hash(state);
        }
    }
}

impl From<f32> for EquatableF32 {
    fn from(value: f32) -> Self {
        Self(value)
    }
}

impl From<EquatableF32> for f32 {
    fn from(value: EquatableF32) -> Self {
        value.0
    }
}

impl Display for EquatableF32 {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        self.0.fmt(f)
    }
}

#[derive(Clone, Copy, PartialEq, Eq, Hash)]
pub struct Position {
    pub x: EquatableF32,
    pub y: EquatableF32,
    pub z: EquatableF32,
    pub w: Option<EquatableF32>,
}

impl From<&[f32; 3]> for Position {
    fn from(value: &[f32; 3]) -> Self {
        Position {
            x: value[0].into(),
            y: value[1].into(),
            z: value[2].into(),
            w: None,
        }
    }
}

impl From<&[f32; 4]> for Position {
    fn from(value: &[f32; 4]) -> Self {
        Position {
            x: value[0].into(),
            y: value[1].into(),
            z: value[2].into(),
            w: Some(value[3].into()),
        }
    }
}

impl Display for Position {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self.w {
            Some(w) => write!(f, "{} {} {} {}", self.x, self.y, self.z, w),
            None => write!(f, "{} {} {}", self.x, self.y, self.z),
        }
    }
}

#[derive(Clone, Copy, PartialEq, Eq, Hash)]
pub enum TexCoord {
    OneD(EquatableF32),
    TwoD([EquatableF32; 2]),
    ThreeD([EquatableF32; 3]),
}

impl From<f32> for TexCoord {
    fn from(value: f32) -> Self {
        Self::OneD(value.into())
    }
}

impl From<&[f32; 2]> for TexCoord {
    fn from(value: &[f32; 2]) -> Self {
        Self::TwoD(value.map(EquatableF32::from))
    }
}

impl From<&[f32; 3]> for TexCoord {
    fn from(value: &[f32; 3]) -> Self {
        Self::ThreeD(value.map(EquatableF32::from))
    }
}

impl Display for TexCoord {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::OneD(u) => write!(f, "{}", u),
            Self::TwoD(uv) => write!(f, "{} {}", uv[0], uv[1]),
            Self::ThreeD(uvw) => write!(f, "{} {} {}", uvw[0], uvw[1], uvw[2]),
        }
    }
}

#[repr(transparent)]
#[derive(Clone, Copy, PartialEq, Eq, Hash)]
pub struct Normal([EquatableF32; 3]);

impl From<&[f32; 3]> for Normal {
    fn from(value: &[f32; 3]) -> Self {
        Self(value.map(EquatableF32::from))
    }
}

impl Display for Normal {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{} {} {}", self.0[0], self.0[1], self.0[2])
    }
}

#[derive(FromPrimitive)]
pub enum PrimitiveCategory {
    Triangle,
    Line,
    Point,
}

impl Display for PrimitiveCategory {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::Triangle => f.write_char('f'),
            Self::Line => f.write_char('l'),
            Self::Point => f.write_char('p'),
        }
    }
}

pub struct WavefrontPrimitive {
    pub category: PrimitiveCategory,
    pub vertices: SmallVec<[VertexInfo; 3]>,
}

#[derive(Clone, Copy)]
pub struct VertexInfo {
    position_idx: u32,
    tex_coord_idx: Option<u32>,
    normal_idx: Option<u32>,
}

impl Display for VertexInfo {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        if let Some(tex_coord_idx) = self.tex_coord_idx {
            write!(f, "{}/{}", self.position_idx, tex_coord_idx)?;
            if let Some(normal_idx) = self.normal_idx {
                write!(f, "/{}", normal_idx)?;
            }
        } else {
            write!(f, "{}", self.position_idx)?;
            if let Some(normal_idx) = self.normal_idx {
                write!(f, "//{}", normal_idx)?;
            }
        }

        Ok(())
    }
}

#[derive(Debug, Error)]
#[non_exhaustive]
pub enum WavefrontError {
    #[error("no position attribute found")]
    NoPosition,
}
