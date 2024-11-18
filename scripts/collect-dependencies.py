import argparse
import json
import os
import sys

from license_expression import get_spdx_licensing

licensing = get_spdx_licensing()

ADDITIONAL_SUFFIXES = {
    "Apache-2.0": ["apache"],
    "Unicode-3.0": ["unicode"],
}

PREFERRED_ORDER = ['MIT']

class PackageInfo:
    def __init__(self, name, version, authors, license, used_license, used_license_files):
        self.name = name
        self.version = version
        self.authors = authors
        self.license = license
        self.used_license = used_license
        self.used_license_files = used_license_files

def get_license_files(source_dir):
    result = {}
    for file in os.listdir(source_dir):
        if file.casefold().startswith('license'):
            result[file.casefold()] = file
    return result

def find_license_file(license_files, source_dir, license, allow_no_suffix=False):
    if allow_no_suffix:
        file = license_files.get('license')
        if file:
            return license, os.path.join(source_dir, file)

    suffixes = [license.casefold()]
    additional_suffixes = ADDITIONAL_SUFFIXES.get(license)
    if additional_suffixes:
        suffixes += additional_suffixes

    for suffix in suffixes:
        file = license_files.get(f"license-{suffix}")
        if file:
            return license, os.path.join(source_dir, file)

    return None

def get_used_license(expr):
    """Remove OR blocks"""
    if expr.isliteral:
        return str(expr)
    if expr.operator == ' AND ':
        return '(' + ' AND '.join(get_used_license(subexpr) for subexpr in expr.args) + ')'
    if expr.operator == ' OR ':
        subexprs = [get_used_license(subexpr) for subexpr in expr.args]
        for preference in PREFERRED_ORDER:
            if preference in subexprs:
                return preference
    return None

def get_used_license_files(license_files, source_dir, expr):
    if expr.isliteral:
        license_file = find_license_file(license_files, source_dir, str(expr))
        if not license_file:
            return None
        return [license_file]

    if expr.operator != ' AND ':
        return None

    result = []
    for subexpr in expr.args:
        used_license_files = get_used_license_files(license_files, source_dir, subexpr)
        if not used_license_files:
            return None
        result += used_license_files
    return result

def collect_licenses(metadata, this_package):
    package_info = []
    this_package_name = None
    this_package_version = None
    for package in metadata["packages"]:
        name = package["name"]
        version = package["version"]

        if name == this_package:
            this_package_name = name
            this_package_version = version
            continue

        authors = package["authors"]

        manifest_path = package["manifest_path"]
        source_dir = os.path.dirname(manifest_path)
        license_files = get_license_files(source_dir)
        if not license_files:
            raise RuntimeError(f"No licenses found for {name} {version}")

        license = package["license"].replace("/", " OR ")
        if licensing.validate(license).errors:
            raise RuntimeError(f"Could not validate license for {name} {version}: {license}")

        parsed_license = licensing.parse(license).simplify()
        if parsed_license.isliteral:
            used_license = parsed_license
            license_file = find_license_file(license_files, source_dir, str(parsed_license), allow_no_suffix=True)
            if not license_file:
                raise RuntimeError(f"Could not find license file for {name} {version}: {parsed_license}")
            used_license_files = [license_file]
        else:
            used_license = get_used_license(parsed_license)
            if not used_license:
                raise RuntimeError(f"Could not determine used license for {name} {version}: {parsed_license}")
            used_license = licensing.parse(used_license).simplify()
            used_license_files = get_used_license_files(license_files, source_dir, used_license)

        package_info.append(PackageInfo(name, version, authors, license, used_license, used_license_files))

    if this_package_name is None or this_package_version is None:
        raise RuntimeError("Could not find info for this package")

    return package_info, this_package_name, this_package_version

def write_info(f, package_name, package_version, package_info):
    print(f"# {package_name} ({package_version})", file=f)
    print(file=f)
    print("The following dependencies have been used:", file=f)
    print(file=f)
    for package in package_info:
        print(f"- {package.name} ({package.version})", file=f)

    print(file=f)
    for package in package_info:
        print(f"## {package.name} ({package.version})", file=f)
        print(file=f)
        if package.authors:
            print("Authors:", file=f)
            print(file=f)
            for author in package.authors:
                print(f"- {author}", file=f)
            print(file=f)

        if licensing.is_equivalent(package.license, package.used_license):
            print(f"Licensed and used as `{package.license}`", file=f)
        else:
            print(f"Licensed as `{package.license}`, used as `{package.used_license}`", file=f)

        print(file=f)
        for license, license_file in package.used_license_files:
            print(f"### {license} license", file=f)
            with open(license_file, 'rt', encoding='utf-8') as lf:
                for line in lf:
                    print('    ', line, sep='', end='', file=f)
            print(file=f)

parser = argparse.ArgumentParser()
parser.add_argument('-p', '--package-name', required=True)
parser.add_argument('-o', '--output', required=True)
args = parser.parse_args()

packages = []

metadata = json.load(sys.stdin)
package_info, package_name, package_version = collect_licenses(metadata, args.package_name)
with open(args.output, 'wt', encoding='utf-8') as f:
    write_info(f, package_name, package_version, package_info)
