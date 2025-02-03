#![forbid(unsafe_code)]

use std::{
    path::{Path, PathBuf},
    process::ExitCode,
};

use clap::Parser;

#[derive(Debug, Parser)]
#[command(name = "cmodconvert")]
#[command(version)]
#[command(about = "Converts Celestia CMOD to Wavefront OBJ/MTL")]
struct Args {
    #[arg(help = "Path to input .cmod file")]
    input_file: PathBuf,

    #[arg(short = 'o', long, help = "Path to output .obj file")]
    output_obj: Option<PathBuf>,

    #[arg(short = 'm', long, help = "Path to output .mtl file")]
    output_mtl: Option<PathBuf>,
}

fn output_path(
    path: Option<PathBuf>,
    input_path: &Path,
    extension: &str,
) -> Result<PathBuf, Box<dyn std::error::Error>> {
    if let Some(p) = path {
        return Ok(p);
    }

    let mut path = input_path.to_path_buf();
    if path.set_extension(extension) {
        Ok(path)
    } else {
        Err(format!("Could not generate output filename with extension {extension}").into())
    }
}

fn run(args: Args) -> Result<(), Box<dyn std::error::Error>> {
    let output_obj = output_path(args.output_obj, &args.input_file, "obj")?;
    let output_mtl = output_path(args.output_mtl, &args.input_file, "mtl")?;

    cmodconvert::convert_cmod(args.input_file, output_obj, output_mtl)
}

fn main() -> ExitCode {
    let args = Args::parse();
    match run(args) {
        Ok(_) => ExitCode::from(0),
        Err(err) => {
            eprintln!("Error: {err}");
            ExitCode::from(1)
        }
    }
}
