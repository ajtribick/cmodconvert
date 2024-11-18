use core::str;
use std::io::BufRead;

use memchr::memchr;
use num_traits::FromPrimitive;

use super::CmodTokenizer;
use crate::cmod::{
    AttributeFormat, AttributeType, BlendMode, CmodError, Color, PrimitiveType, TextureSemantic,
    Token,
};

pub struct TextReader<R: BufRead> {
    reader: TextTokenReader<R>,
}

impl<R: BufRead> TextReader<R> {
    pub fn new(input: R) -> Self {
        Self {
            reader: TextTokenReader::new(input),
        }
    }
}

impl<R: BufRead> CmodTokenizer for TextReader<R> {
    fn read_token(&mut self) -> Result<Option<Token>, CmodError> {
        match self.reader.next_token()? {
            TextToken::Name(name) => name.parse().map(Some),
            TextToken::Eof => Ok(None),
            _ => Err(CmodError::NotAName),
        }
    }

    fn read_texture_semantic(&mut self) -> Result<TextureSemantic, CmodError> {
        if let TextToken::Integer(value) = self.reader.next_token()? {
            let value = u16::try_from(value).or(Err(CmodError::BadNumber))?;
            TextureSemantic::from_u16(value).ok_or(CmodError::InvalidTextureSemantic(value))
        } else {
            Err(CmodError::NotAnInteger)
        }
    }

    fn read_blend_mode(&mut self) -> Result<BlendMode, CmodError> {
        match self.reader.next_token()? {
            TextToken::Name(name) => name.parse(),
            _ => Err(CmodError::NotAName),
        }
    }

    fn read_attribute_type(&mut self) -> Result<Option<AttributeType>, CmodError> {
        if let TextToken::Name(name) = self.reader.next_token()? {
            if name.as_ref() == Token::END_VERTEXDESC {
                Ok(None)
            } else {
                name.parse().map(Some)
            }
        } else {
            Err(CmodError::NotAName)
        }
    }

    fn read_attribute_format(&mut self) -> Result<AttributeFormat, CmodError> {
        match self.reader.next_token()? {
            TextToken::Name(name) => name.parse(),
            _ => Err(CmodError::NotAName),
        }
    }

    fn read_primitive_type(&mut self) -> Result<Option<PrimitiveType>, CmodError> {
        if let TextToken::Name(name) = self.reader.next_token()? {
            if name.as_ref() == Token::END_MESH {
                Ok(None)
            } else {
                name.parse().map(Some)
            }
        } else {
            Err(CmodError::NotAName)
        }
    }

    fn read_color(&mut self) -> Result<Color, CmodError> {
        let red = self.read_f32()?;
        let green = self.read_f32()?;
        let blue = self.read_f32()?;
        Ok(Color { red, green, blue })
    }

    fn read_float1(&mut self) -> Result<f32, CmodError> {
        self.read_f32()
    }

    fn read_string(&mut self) -> Result<Box<str>, CmodError> {
        match self.reader.next_token()? {
            TextToken::Quoted(s) => Ok(s),
            _ => Err(CmodError::NotAString),
        }
    }

    fn read_u32(&mut self) -> Result<u32, CmodError> {
        match self.reader.next_token()? {
            TextToken::Integer(n) => Ok(n),
            _ => Err(CmodError::NotAnInteger),
        }
    }

    fn read_u32_vec(&mut self, dst: &mut [u32]) -> Result<(), CmodError> {
        for n in dst.iter_mut() {
            *n = self.read_u32()?;
        }

        Ok(())
    }

    fn read_f32(&mut self) -> Result<f32, CmodError> {
        match self.reader.next_token()? {
            TextToken::Integer(n) => Ok(n as f32),
            TextToken::Float(f) => Ok(f),
            _ => Err(CmodError::NotAFloat),
        }
    }

    fn read_f32_vec(&mut self, dst: &mut [f32]) -> Result<(), CmodError> {
        for n in dst.iter_mut() {
            *n = self.read_f32()?;
        }

        Ok(())
    }

    fn read_ub4(&mut self) -> Result<[u8; 4], CmodError> {
        let mut value = [0; 4];
        for n in value.iter_mut() {
            *n = u8::try_from(self.read_u32()?).or(Err(CmodError::NotAByte))?;
        }

        Ok(value)
    }
}

struct TextTokenReader<R: BufRead> {
    input: R,
}

enum ParseState {
    Normal,
    Comment,
}

enum NumberState {
    Begin,
    AfterSign,
    AfterPoint,
    AfterExponent,
    AfterExponentSign,
}

#[cfg_attr(test, derive(Debug, PartialEq))]
enum TextToken {
    Name(Box<str>),
    Quoted(Box<str>),
    Integer(u32),
    Float(f32),
    Eof,
}

fn is_name_char(c: u8) -> bool {
    c == b'_' || c.is_ascii_digit() || c.is_ascii_alphabetic()
}

impl<R: BufRead> TextTokenReader<R> {
    pub fn new(input: R) -> Self {
        Self { input }
    }

    pub fn next_token(&mut self) -> Result<TextToken, CmodError> {
        let mut parse_state = ParseState::Normal;
        loop {
            let buffer = self.input.fill_buf()?;
            if buffer.is_empty() {
                return Ok(TextToken::Eof);
            }

            let mut pos = 0;
            while pos < buffer.len() {
                match parse_state {
                    ParseState::Normal => match buffer[pos] {
                        b'\t' | b'\r' | b'\n' | b' ' => (),
                        b'#' => parse_state = ParseState::Comment,
                        b'"' => {
                            self.input.consume(pos + 1);
                            return self.read_quoted();
                        }
                        b'0'..=b'9' | b'+' | b'-' | b'.' => {
                            self.input.consume(pos);
                            return self.read_number();
                        }
                        b'A'..=b'Z' | b'a'..=b'z' | b'_' => {
                            self.input.consume(pos);
                            return self.read_name();
                        }
                        _ => return Err(CmodError::UnexpectedChar),
                    },
                    ParseState::Comment => match memchr(b'\n', &buffer[pos..]) {
                        Some(line_pos) => {
                            pos += line_pos;
                            parse_state = ParseState::Normal;
                        }
                        None => break,
                    },
                }

                pos += 1;
            }

            let buffer_len = buffer.len();
            self.input.consume(buffer_len);
        }
    }

    fn read_quoted(&mut self) -> Result<TextToken, CmodError> {
        let mut quoted = Vec::new();
        loop {
            let buffer = self.input.fill_buf()?;
            if buffer.is_empty() {
                return Err(CmodError::UnclosedString);
            }

            if let Some(pos) = memchr(b'"', buffer) {
                quoted.extend_from_slice(&buffer[..pos]);
                self.input.consume(pos + 1);
                break;
            }

            quoted.extend_from_slice(buffer);
            let buffer_len = buffer.len();
            self.input.consume(buffer_len);
        }

        let quoted = String::from_utf8(quoted).or(Err(CmodError::InvalidUTF8))?;
        Ok(TextToken::Quoted(quoted.into_boxed_str()))
    }

    fn read_number(&mut self) -> Result<TextToken, CmodError> {
        let mut num_buffer = [0; 256];
        let mut num_pos = 0;
        let mut number_state = NumberState::Begin;
        'outer: loop {
            let buffer = self.input.fill_buf()?;
            if buffer.is_empty() {
                break;
            }

            let mut pos = 0;
            while pos < buffer.len() {
                let c = buffer[pos];
                match c {
                    b'+' | b'-' => match number_state {
                        NumberState::Begin => {
                            num_buffer[0] = c;
                            number_state = NumberState::AfterSign;
                        }
                        NumberState::AfterExponent => {
                            num_buffer[num_pos] = c;
                            number_state = NumberState::AfterExponentSign;
                        }
                        _ => {
                            self.input.consume(pos);
                            break 'outer;
                        }
                    },
                    b'0'..=b'9' => {
                        num_buffer[num_pos] = c;
                        number_state = match number_state {
                            NumberState::Begin => NumberState::AfterSign,
                            NumberState::AfterExponent => NumberState::AfterExponentSign,
                            _ => number_state,
                        };
                    }
                    b'.' => match number_state {
                        NumberState::Begin => {
                            num_buffer[0] = b'0';
                            num_buffer[1] = b'.';
                            num_pos = 1;
                            number_state = NumberState::AfterPoint;
                        }
                        NumberState::AfterSign => {
                            num_buffer[num_pos] = c;
                            number_state = NumberState::AfterPoint;
                        }
                        _ => {
                            self.input.consume(pos);
                            break 'outer;
                        }
                    },
                    b'e' | b'E' => {
                        if matches!(
                            number_state,
                            NumberState::AfterSign | NumberState::AfterPoint
                        ) {
                            num_buffer[num_pos] = b'e';
                            number_state = NumberState::AfterExponent;
                        } else {
                            self.input.consume(pos);
                            break 'outer;
                        }
                    }
                    _ => {
                        self.input.consume(pos);
                        break 'outer;
                    }
                }

                pos += 1;
                num_pos += 1;
            }

            let buffer_len = buffer.len();
            self.input.consume(buffer_len);
        }

        let num_buffer = str::from_utf8(&num_buffer[..num_pos]).or(Err(CmodError::InvalidUTF8))?;
        if let Ok(value) = num_buffer.parse::<u32>() {
            return Ok(TextToken::Integer(value));
        }

        num_buffer
            .parse::<f32>()
            .map_or(Err(CmodError::BadNumber), |f| Ok(TextToken::Float(f)))
    }

    fn read_name(&mut self) -> Result<TextToken, CmodError> {
        const TEXTURE: &[u8; 7] = b"texture";
        let mut name = Vec::new();
        loop {
            let buffer = self.input.fill_buf()?;
            if buffer.is_empty() {
                break;
            }

            // in the text version of CMOD, the texture semantic is joined to the "texture" name
            // so we handle this case specially
            if name.len() < TEXTURE.len()
                && name.starts_with(&TEXTURE[..name.len()])
                && buffer.starts_with(&TEXTURE[name.len()..])
            {
                self.input.consume(TEXTURE.len() - name.len());
                name = TEXTURE.into();
                break;
            }

            if let Some(pos) = buffer.iter().position(|&c| !is_name_char(c)) {
                name.extend_from_slice(&buffer[..pos]);
                self.input.consume(pos);
                break;
            }

            name.extend_from_slice(buffer);
            let buffer_len = buffer.len();
            self.input.consume(buffer_len);
        }

        let name = String::from_utf8(name).or(Err(CmodError::InvalidUTF8))?;
        Ok(TextToken::Name(name.into_boxed_str()))
    }
}

#[cfg(test)]
mod test {
    use std::io::Cursor;

    use super::*;

    #[test]
    fn read_eof() {
        let src = br#"
# comment 1
# comment 2

"#;
        let cursor = Cursor::new(src);
        let mut reader = TextTokenReader::new(cursor);
        let token = reader.next_token().expect("read a token");
        assert!(matches!(token, TextToken::Eof));
    }

    #[test]
    fn read_name() {
        let src = br#"
# here is a comment
   xyzzy
"#;
        let cursor = Cursor::new(src);
        let mut reader = TextTokenReader::new(cursor);
        let token = reader.next_token().expect("read a token");
        if let TextToken::Name(name) = token {
            assert_eq!(name.as_ref(), "xyzzy");
        } else {
            panic!("not a Name token");
        }
    }

    #[test]
    fn read_texture_name() {
        let src = "texture1";
        let cursor = Cursor::new(src);
        let mut reader = TextTokenReader::new(cursor);

        let expected_tokens = [TextToken::Name(Box::from("texture")), TextToken::Integer(1)];

        let mut tokens = Vec::new();
        loop {
            let token = reader.next_token().expect("read a token");
            if token == TextToken::Eof {
                break;
            }

            tokens.push(token);
        }

        assert_eq!(tokens, expected_tokens);
    }

    #[test]
    fn read_quoted() {
        let src = br#"
# here is a comment
    "texturefile.jpg"
"#;
        let cursor = Cursor::new(src);
        let mut reader = TextTokenReader::new(cursor);
        let token = reader.next_token().expect("read a token");
        if let TextToken::Quoted(quoted) = token {
            assert_eq!(quoted.as_ref(), "texturefile.jpg");
        } else {
            panic!("not a Quoted token");
        }
    }

    #[test]
    fn read_integer() {
        let src = br#"
        # here is a comment

42
        "#;
        let cursor = Cursor::new(src);
        let mut reader = TextTokenReader::new(cursor);
        let token = reader.next_token().expect("read a token");
        if let TextToken::Integer(number) = token {
            assert_eq!(number, 42);
        } else {
            panic!("not a Number token");
        }
    }

    #[test]
    fn read_float() {
        let src = br#"
        # here is a comment

-3.25e+4
        "#;
        let cursor = Cursor::new(src);
        let mut reader = TextTokenReader::new(cursor);
        let token = reader.next_token().expect("read a token");
        if let TextToken::Float(number) = token {
            assert_eq!(number, -3.25e+4);
        } else {
            panic!("not a Number token");
        }
    }
}
