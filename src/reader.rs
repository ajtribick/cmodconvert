use crate::cmod::{
    AttributeData, AttributeFormat, AttributeType, BlendMode, CmodData, CmodError, Color, Material,
    Mesh, Primitive, PrimitiveType, TextureSemantic, Token, VertexAttribute, VertexDescriptor,
};

mod binaryread;
mod textread;

pub use binaryread::BinaryReader;
pub use textread::TextReader;

pub trait CmodTokenizer {
    fn read_token(&mut self) -> Result<Option<Token>, CmodError>;
    fn read_texture_semantic(&mut self) -> Result<TextureSemantic, CmodError>;
    fn read_blend_mode(&mut self) -> Result<BlendMode, CmodError>;
    fn read_attribute_type(&mut self) -> Result<Option<AttributeType>, CmodError>;
    fn read_attribute_format(&mut self) -> Result<AttributeFormat, CmodError>;
    fn read_primitive_type(&mut self) -> Result<Option<PrimitiveType>, CmodError>;

    fn read_color(&mut self) -> Result<Color, CmodError>;
    fn read_float1(&mut self) -> Result<f32, CmodError>;
    fn read_string(&mut self) -> Result<Box<str>, CmodError>;

    fn read_u32(&mut self) -> Result<u32, CmodError>;
    fn read_u32_vec(&mut self, dst: &mut [u32]) -> Result<(), CmodError>;
    fn read_f32(&mut self) -> Result<f32, CmodError>;
    fn read_f32_vec(&mut self, dst: &mut [f32]) -> Result<(), CmodError>;
    fn read_ub4(&mut self) -> Result<[u8; 4], CmodError>;
}

pub struct CmodReader<'a> {
    reader: &'a mut dyn CmodTokenizer,
}

impl<'a> CmodReader<'a> {
    pub fn new(reader: &'a mut dyn CmodTokenizer) -> Self {
        Self { reader }
    }

    pub fn read_cmod(&mut self) -> Result<CmodData, CmodError> {
        let mut cmod_data = CmodData::default();

        loop {
            match self.reader.read_token()? {
                Some(Token::Material) => cmod_data.materials.push(self.read_material()?),
                Some(Token::Mesh) => break,
                unexpected_token => return Err(CmodError::UnexpectedToken(unexpected_token)),
            }
        }

        let material_count =
            u32::try_from(cmod_data.materials.len()).or(Err(CmodError::InvalidMaterialCount))?;
        cmod_data.meshes.push(self.read_mesh(material_count)?);

        loop {
            match self.reader.read_token()? {
                Some(Token::Mesh) => cmod_data.meshes.push(self.read_mesh(material_count)?),
                None => break,
                unexpected_token => return Err(CmodError::UnexpectedToken(unexpected_token)),
            }
        }

        Ok(cmod_data)
    }

    fn read_material(&mut self) -> Result<Material, CmodError> {
        let mut material = Material::default();
        loop {
            match self.reader.read_token()? {
                Some(Token::Diffuse) => material.diffuse = Some(self.reader.read_color()?),
                Some(Token::Specular) => material.specular = Some(self.reader.read_color()?),
                Some(Token::Emissive) => material.emissive = Some(self.reader.read_color()?),
                Some(Token::SpecularPower) => {
                    material.specular_power = Some(self.reader.read_float1()?)
                }
                Some(Token::Opacity) => material.opacity = Some(self.reader.read_float1()?),
                Some(Token::Blend) => material.blend_mode = Some(self.reader.read_blend_mode()?),
                Some(Token::Texture) => self.read_texture(&mut material.textures)?,
                Some(Token::EndMaterial) => return Ok(material),
                unexpected_token => return Err(CmodError::UnexpectedToken(unexpected_token)),
            }
        }
    }

    fn read_texture(&mut self, textures: &mut [Option<Box<str>>; 4]) -> Result<(), CmodError> {
        let semantic = self.reader.read_texture_semantic()?;
        textures[semantic as usize] = Some(self.reader.read_string()?);
        Ok(())
    }

    fn read_mesh(&mut self, material_count: u32) -> Result<Mesh, CmodError> {
        let descriptors = self.read_vertex_descriptors()?;
        let (vertex_count, attributes) = self.read_vertices(descriptors)?;
        let primitives = self.read_primitives(material_count, vertex_count)?;
        Ok(Mesh {
            attributes,
            primitives,
            vertex_count: vertex_count as usize,
        })
    }

    fn read_vertex_descriptors(&mut self) -> Result<Vec<VertexDescriptor>, CmodError> {
        self.read_expected_token(Token::VertexDesc)?;
        let mut descriptors: Vec<VertexDescriptor> = Vec::new();
        loop {
            match self.reader.read_attribute_type()? {
                Some(attribute_type) => {
                    if descriptors
                        .iter()
                        .any(|a| a.attribute_type == attribute_type)
                    {
                        return Err(CmodError::DuplicateAttributeType);
                    }

                    let format = self.reader.read_attribute_format()?;
                    if !attribute_type.is_valid_format(format) {
                        return Err(CmodError::UnexpectedAttributeFormat(format));
                    }

                    descriptors.push(VertexDescriptor {
                        attribute_type,
                        format,
                    });
                }
                None => return Ok(descriptors),
            }
        }
    }

    fn read_vertices(
        &mut self,
        descriptors: Vec<VertexDescriptor>,
    ) -> Result<(u32, Vec<VertexAttribute>), CmodError> {
        self.read_expected_token(Token::Vertices)?;

        let vertex_count = self.reader.read_u32()?;
        if vertex_count == 0 {
            return Err(CmodError::InvalidVertexCount);
        }

        let capacity = usize::try_from(vertex_count).or(Err(CmodError::InvalidVertexCount))?;
        let mut attributes: Vec<_> = descriptors
            .into_iter()
            .map(|descriptor| VertexAttribute::new(&descriptor, capacity))
            .collect();

        for _ in 0..vertex_count {
            for attribute in attributes.iter_mut() {
                self.read_attribute(attribute)?;
            }
        }

        Ok((vertex_count, attributes))
    }

    fn read_attribute(&mut self, attribute: &mut VertexAttribute) -> Result<(), CmodError> {
        match attribute.data {
            AttributeData::Float1(ref mut v) => {
                v.push(self.reader.read_f32()?);
            }
            AttributeData::Float2(ref mut v) => {
                v.push(Default::default());
                if let Err(err) = self.reader.read_f32_vec(v.last_mut().unwrap()) {
                    v.pop();
                    return Err(err);
                }
            }
            AttributeData::Float3(ref mut v) => {
                v.push(Default::default());
                if let Err(err) = self.reader.read_f32_vec(v.last_mut().unwrap()) {
                    v.pop();
                    return Err(err);
                }
            }
            AttributeData::Float4(ref mut v) => {
                v.push(Default::default());
                if let Err(err) = self.reader.read_f32_vec(v.last_mut().unwrap()) {
                    v.pop();
                    return Err(err);
                }
            }
            AttributeData::UByte4(ref mut v) => {
                v.push(self.reader.read_ub4()?);
            }
        }

        Ok(())
    }

    fn read_primitives(
        &mut self,
        material_count: u32,
        vertex_count: u32,
    ) -> Result<Vec<Primitive>, CmodError> {
        let mut primitives: Vec<Primitive> = Vec::new();
        loop {
            match self.reader.read_primitive_type()? {
                Some(primitive_type) => {
                    let material_index = self.reader.read_u32()?;
                    if material_index >= material_count {
                        return Err(CmodError::InvalidMaterial(material_index));
                    }

                    let index_count = usize::try_from(self.reader.read_u32()?)
                        .or(Err(CmodError::InvalidIndexCount))?;
                    if index_count < primitive_type.min_points() {
                        return Err(CmodError::InvalidIndexCount);
                    }

                    let mut indices = vec![0; index_count];
                    self.reader.read_u32_vec(&mut indices)?;
                    if let Some(invalid_index) =
                        indices.iter().copied().find(|&idx| idx >= vertex_count)
                    {
                        return Err(CmodError::InvalidIndex(invalid_index));
                    }

                    primitives.push(Primitive {
                        primitive_type,
                        material_index,
                        indices,
                    });
                }
                None => return Ok(primitives),
            }
        }
    }

    fn read_expected_token(&mut self, expected_token: Token) -> Result<(), CmodError> {
        let token = self.reader.read_token()?;
        if token == Some(expected_token) {
            Ok(())
        } else {
            Err(CmodError::UnexpectedToken(token))
        }
    }
}
