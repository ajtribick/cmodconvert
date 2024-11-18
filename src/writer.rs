use std::{
    borrow::Cow,
    fs::File,
    io::{self, BufWriter, ErrorKind, Write},
    path::Path,
};

use pathdiff::diff_paths;

use crate::{cmod::Material, wavefront::WavefrontMesh};

pub fn write_mtl(path: impl AsRef<Path>, materials: &[Material]) -> io::Result<()> {
    let output = File::create(path)?;
    let mut output = BufWriter::new(output);
    for (idx, material) in materials.iter().enumerate() {
        writeln!(output, "newmtl material{idx}")?;
        if let Some(diffuse) = &material.diffuse {
            writeln!(output, "Kd {diffuse}")?;
        }

        if let Some(emissive) = &material.emissive {
            writeln!(output, "Ka {emissive}")?;
        }

        if let Some(specular) = &material.specular {
            writeln!(output, "Ks {specular}")?;
        }

        if let Some(specular_power) = material.specular_power {
            writeln!(output, "Ns {specular_power}")?;
        }

        if let Some(opacity) = material.opacity {
            writeln!(output, "d {opacity}")?;
        }

        if let Some(diffuse_texture) = material.textures[Material::DIFFUSE].as_deref() {
            writeln!(output, "map_Kd {}", diffuse_texture)?;
        }

        if let Some(emissive_texture) = material.textures[Material::EMISSIVE].as_deref() {
            writeln!(output, "map_Ka {}", emissive_texture)?;
        }

        if let Some(specular_texture) = material.textures[Material::SPECULAR].as_deref() {
            writeln!(output, "map_Ks {}", specular_texture)?;
        }
    }

    Ok(())
}

fn write_mtl_path(
    output: &mut impl std::io::Write,
    mtl_path: impl AsRef<Path>,
    obj_path: impl AsRef<Path>,
) -> io::Result<()> {
    let mtl_path = match obj_path.as_ref().parent() {
        Some(base_path) => diff_paths(mtl_path.as_ref(), base_path)
            .map_or_else(|| Cow::from(mtl_path.as_ref()), Cow::from),
        None => Cow::from(mtl_path.as_ref()),
    };

    let mtl_path = mtl_path
        .to_str()
        .ok_or_else(|| io::Error::new(ErrorKind::InvalidData, "invalid path"))?;
    writeln!(output, "mtllib {mtl_path}")?;

    Ok(())
}

pub fn write_obj(
    obj_path: impl AsRef<Path>,
    mtl_path: impl AsRef<Path>,
    mesh: &WavefrontMesh,
) -> io::Result<()> {
    let output: File = File::create(obj_path.as_ref())?;
    let mut output = BufWriter::new(output);

    write_mtl_path(&mut output, mtl_path, obj_path)?;

    for position in &mesh.positions {
        writeln!(output, "v {position}")?;
    }

    for tex_coord in &mesh.tex_coords {
        writeln!(output, "vt {tex_coord}")?;
    }

    for normal in &mesh.normals {
        writeln!(output, "vn {normal}")?;
    }

    let primitive_groups = mesh
        .primitive_groups
        .iter()
        .enumerate()
        .filter(|(_, group)| !group.is_empty());

    for (idx, primitives) in primitive_groups {
        writeln!(output, "usemtl material{idx}")?;
        for primitive in primitives {
            write!(output, "{}", primitive.category)?;
            for vertex in &primitive.vertices {
                write!(output, " {}", vertex)?;
            }
            writeln!(output)?;
        }
    }

    Ok(())
}
