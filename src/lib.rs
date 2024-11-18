#![forbid(unsafe_code)]

use std::{
    fs::File,
    io::{BufReader, Read},
    path::Path,
};

use crate::{
    cmod::CmodError,
    reader::{BinaryReader, CmodReader, TextReader},
    wavefront::WavefrontMesh,
    writer::{write_mtl, write_obj},
};

mod cmod;
mod reader;
mod wavefront;
mod writer;

const ASCII_HEADER: &[u8; 16] = b"#celmodel__ascii";
const BINARY_HEADER: &[u8; 16] = b"#celmodel_binary";

pub fn convert_cmod(
    input_file: impl AsRef<Path>,
    output_obj: impl AsRef<Path>,
    output_mtl: impl AsRef<Path>,
) -> Result<(), Box<dyn std::error::Error>> {
    let input = File::open(input_file)?;
    let mut input = BufReader::new(input);
    let mut header = [0; 16];
    input.read_exact(&mut header)?;
    let cmod_data = match &header {
        BINARY_HEADER => {
            let mut reader = BinaryReader::new(input);
            let mut reader = CmodReader::new(&mut reader);
            reader.read_cmod()?
        }
        ASCII_HEADER => {
            let mut reader = TextReader::new(input);
            let mut reader = CmodReader::new(&mut reader);
            reader.read_cmod()?
        }
        _ => return Err(CmodError::BadSignature.into()),
    };

    let wavefront_mesh = WavefrontMesh::try_new(&cmod_data)?;

    write_mtl(output_mtl.as_ref(), &wavefront_mesh.materials)?;
    write_obj(output_obj, output_mtl, &wavefront_mesh)?;

    Ok(())
}
