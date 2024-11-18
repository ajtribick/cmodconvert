use std::io::{self, Read};

use byteorder::{LittleEndian, ReadBytesExt};
use num_traits::FromPrimitive;

use crate::cmod::{
    AttributeFormat, AttributeType, BlendMode, CmodError, Color, DataType, PrimitiveType,
    TextureSemantic, Token,
};

use super::CmodTokenizer;

pub struct BinaryReader<R: Read> {
    input: R,
}

impl<R: Read> BinaryReader<R> {
    pub fn new(input: R) -> Self {
        Self { input }
    }

    fn read_expected_data_type(&mut self, expected_type: DataType) -> Result<(), CmodError> {
        let token = self.input.read_u16::<LittleEndian>()?;
        let data_type =
            DataType::from_u16(token).ok_or_else(|| CmodError::InvalidDataType(token))?;
        if data_type == expected_type {
            Ok(())
        } else {
            Err(CmodError::UnexpectedDataType(data_type))
        }
    }
}

impl<R: Read> CmodTokenizer for BinaryReader<R> {
    fn read_token(&mut self) -> Result<Option<Token>, CmodError> {
        match self.input.read_u16::<LittleEndian>() {
            Ok(token) => Token::from_u16(token)
                .map(Some)
                .ok_or_else(|| CmodError::InvalidToken(token)),
            Err(err) if err.kind() == io::ErrorKind::UnexpectedEof => Ok(None),
            Err(err) => Err(err.into()),
        }
    }

    fn read_texture_semantic(&mut self) -> Result<TextureSemantic, CmodError> {
        let token = self.input.read_u16::<LittleEndian>()?;
        TextureSemantic::from_u16(token).ok_or_else(|| CmodError::InvalidTextureSemantic(token))
    }

    fn read_blend_mode(&mut self) -> Result<BlendMode, CmodError> {
        let token = self.input.read_u16::<LittleEndian>()?;
        BlendMode::from_u16(token).ok_or_else(|| CmodError::InvalidBlendMode(token))
    }

    fn read_attribute_type(&mut self) -> Result<Option<AttributeType>, CmodError> {
        let token = self.input.read_u16::<LittleEndian>()?;
        if token == Token::EndVertexDesc as u16 {
            return Ok(None);
        }

        let attribute_type =
            AttributeType::from_u16(token).ok_or_else(|| CmodError::InvalidAttributeType(token))?;

        Ok(Some(attribute_type))
    }

    fn read_attribute_format(&mut self) -> Result<AttributeFormat, CmodError> {
        let token = self.input.read_u16::<LittleEndian>()?;
        AttributeFormat::from_u16(token).ok_or_else(|| CmodError::InvalidAttributeFormat(token))
    }

    fn read_primitive_type(&mut self) -> Result<Option<PrimitiveType>, CmodError> {
        let token = self.input.read_u16::<LittleEndian>()?;
        if token == Token::EndMesh as u16 {
            return Ok(None);
        }

        let primitive_type =
            PrimitiveType::from_u16(token).ok_or_else(|| CmodError::InvalidPrimitiveType(token))?;

        Ok(Some(primitive_type))
    }

    fn read_color(&mut self) -> Result<Color, CmodError> {
        self.read_expected_data_type(DataType::Color)?;

        let red = self.input.read_f32::<LittleEndian>()?;
        let green = self.input.read_f32::<LittleEndian>()?;
        let blue = self.input.read_f32::<LittleEndian>()?;

        Ok(Color { red, green, blue })
    }

    fn read_float1(&mut self) -> Result<f32, CmodError> {
        self.read_expected_data_type(DataType::Float1)?;
        self.input
            .read_f32::<LittleEndian>()
            .map_err(CmodError::IOError)
    }

    fn read_string(&mut self) -> Result<Box<str>, CmodError> {
        self.read_expected_data_type(DataType::String)?;

        let length = self.input.read_u16::<LittleEndian>()? as usize;
        let mut data = vec![0; length];
        self.input.read_exact(&mut data)?;
        String::from_utf8(data).map_or(Err(CmodError::InvalidUTF8), |s| Ok(s.into_boxed_str()))
    }

    fn read_u32(&mut self) -> Result<u32, CmodError> {
        self.input
            .read_u32::<LittleEndian>()
            .map_err(CmodError::from)
    }

    fn read_u32_vec(&mut self, dst: &mut [u32]) -> Result<(), CmodError> {
        self.input
            .read_u32_into::<LittleEndian>(dst)
            .map_err(CmodError::from)
    }

    fn read_f32(&mut self) -> Result<f32, CmodError> {
        self.input
            .read_f32::<LittleEndian>()
            .map_err(CmodError::from)
    }

    fn read_f32_vec(&mut self, dst: &mut [f32]) -> Result<(), CmodError> {
        self.input
            .read_f32_into::<LittleEndian>(dst)
            .map_err(CmodError::from)
    }

    fn read_ub4(&mut self) -> Result<[u8; 4], CmodError> {
        let mut result = [0; 4];
        self.input.read_exact(&mut result)?;
        Ok(result)
    }
}
