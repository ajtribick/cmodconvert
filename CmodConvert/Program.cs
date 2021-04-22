/* CmodConvert - converts Celestia CMOD files to Wavefront OBJ/MTL format.
 * Copyright (C) 2021  Andrew Tribick
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;
using CmodConvert.IO;
using CmodConvert.Wavefront;

namespace CmodConvert
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Argument<FileInfo>("input-file"),
                new Option<FileInfo?>("--output-file", "The output obj file (defaults to input-file.obj)"),
                new Option<FileInfo?>("--output-mtl", "The output mtl file (defaults to output-file.mtl)"),
            };

            rootCommand.Description = "Convert Celestia cmod files into Wavefront obj/mtl format";

            rootCommand.Handler = CommandHandler.Create<FileInfo, FileInfo?, FileInfo?>(Execute);

            return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
        }

        private static async Task<int> Execute(FileInfo inputFile, FileInfo? outputFile, FileInfo? outputMtl)
        {
            try
            {
                var cmodPath = inputFile.FullName;
                var objPath = outputFile?.FullName ?? Path.ChangeExtension(outputMtl?.FullName ?? cmodPath, "obj");
                var mtlPath = outputMtl?.FullName ?? Path.ChangeExtension(objPath, "mtl");

                var model = await LoadModel(cmodPath).ConfigureAwait(false);
                var wavefrontMesh = WavefrontMesh.Create(model);

                var wavefrontWriter = new WavefrontWriter(objPath, mtlPath);
                await wavefrontWriter.Write(wavefrontMesh).ConfigureAwait(false);

                Console.WriteLine($"Wrote obj: {objPath}");
                Console.WriteLine($"Wrote mtl: {mtlPath}");

                return 0;
            }
            catch (CmodException e)
            {
                Console.Error.WriteLine($"Failed to read CMOD: {e.Message}");
                return 1;
            }
            catch (Exception e)
            {
                Console.WriteLine($"An unexpected error occurred: {e.Message}", e);
                return 1;
            }
        }

        private static async Task<CmodData> LoadModel(string path)
        {
            using var cmodStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan | FileOptions.Asynchronous);
            var reader = CmodReader.Create(cmodStream);
            return await reader.Read().ConfigureAwait(false);
        }
    }
}
