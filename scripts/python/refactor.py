#!/usr/bin/env python3
"""
Python refactoring tool using rope library.
This script is called by Aura's PythonRefactoringService to perform
refactoring operations on Python codebases.

Requirements:
    pip install rope

Usage:
    python refactor.py rename --project /path/to/project --file src/module.py --offset 42 --new-name new_name
    python refactor.py extract-method --project /path/to/project --file src/module.py --start 10 --end 20 --new-name helper_func
    python refactor.py find-references --project /path/to/project --file src/module.py --offset 42
"""

import argparse
import json
import sys
from pathlib import Path
from typing import Any

try:
    import rope.base.project
    from rope.base import libutils
    from rope.refactor.rename import Rename
    from rope.refactor.extract import ExtractMethod, ExtractVariable
    from rope.contrib import findit
except ImportError:
    print(json.dumps({
        "success": False,
        "error": "rope library not installed. Run: pip install rope"
    }))
    sys.exit(1)


def get_project(project_path: str) -> rope.base.project.Project:
    """Get or create a rope project."""
    return rope.base.project.Project(project_path)


def get_resource(project: rope.base.project.Project, file_path: str):
    """Get a rope resource from a file path."""
    # Convert to relative path if absolute
    path = Path(file_path)
    if path.is_absolute():
        try:
            path = path.relative_to(project.address)
        except ValueError:
            pass
    return project.get_resource(str(path))


def rename_symbol(project_path: str, file_path: str, offset: int, new_name: str, preview: bool = False) -> dict[str, Any]:
    """
    Rename a symbol at the given offset.
    
    Args:
        project_path: Root of the Python project
        file_path: Path to the file containing the symbol
        offset: Character offset of the symbol in the file
        new_name: New name for the symbol
        preview: If True, return changes without applying them
        
    Returns:
        Dictionary with success status and changed files
    """
    try:
        project = get_project(project_path)
        resource = get_resource(project, file_path)
        
        # Create rename refactoring
        rename = Rename(project, resource, offset)
        
        # Get the changes
        changes = rename.get_changes(new_name)
        
        if preview:
            # Return preview of changes without applying
            changed_files = []
            for change in changes.get_changed_resources():
                old_content = change.read()
                new_content = changes.get_changed_contents()[change]
                changed_files.append({
                    "file": str(change.path),
                    "oldContent": old_content,
                    "newContent": new_content
                })
            
            return {
                "success": True,
                "preview": True,
                "changedFiles": changed_files,
                "description": changes.description
            }
        else:
            # Apply changes
            project.do(changes)
            
            changed_files = [str(r.path) for r in changes.get_changed_resources()]
            
            return {
                "success": True,
                "preview": False,
                "changedFiles": changed_files,
                "description": changes.description
            }
    except Exception as e:
        return {
            "success": False,
            "error": str(e),
            "errorType": type(e).__name__
        }
    finally:
        if 'project' in locals():
            project.close()


def extract_method(
    project_path: str,
    file_path: str,
    start_offset: int,
    end_offset: int,
    new_name: str,
    preview: bool = False
) -> dict[str, Any]:
    """
    Extract a code region into a new method.
    
    Args:
        project_path: Root of the Python project
        file_path: Path to the file containing the code
        start_offset: Start character offset of the region
        end_offset: End character offset of the region
        new_name: Name for the new method
        preview: If True, return changes without applying them
        
    Returns:
        Dictionary with success status and changed files
    """
    try:
        project = get_project(project_path)
        resource = get_resource(project, file_path)
        
        # Create extract method refactoring
        extract = ExtractMethod(project, resource, start_offset, end_offset)
        
        # Get the changes
        changes = extract.get_changes(new_name)
        
        if preview:
            changed_files = []
            for change in changes.get_changed_resources():
                old_content = change.read()
                new_content = changes.get_changed_contents()[change]
                changed_files.append({
                    "file": str(change.path),
                    "oldContent": old_content,
                    "newContent": new_content
                })
            
            return {
                "success": True,
                "preview": True,
                "changedFiles": changed_files,
                "description": changes.description
            }
        else:
            project.do(changes)
            
            changed_files = [str(r.path) for r in changes.get_changed_resources()]
            
            return {
                "success": True,
                "preview": False,
                "changedFiles": changed_files,
                "description": changes.description
            }
    except Exception as e:
        return {
            "success": False,
            "error": str(e),
            "errorType": type(e).__name__
        }
    finally:
        if 'project' in locals():
            project.close()


def extract_variable(
    project_path: str,
    file_path: str,
    start_offset: int,
    end_offset: int,
    new_name: str,
    preview: bool = False
) -> dict[str, Any]:
    """
    Extract an expression into a variable.
    
    Args:
        project_path: Root of the Python project
        file_path: Path to the file containing the expression
        start_offset: Start character offset of the expression
        end_offset: End character offset of the expression
        new_name: Name for the new variable
        preview: If True, return changes without applying them
        
    Returns:
        Dictionary with success status and changed files
    """
    try:
        project = get_project(project_path)
        resource = get_resource(project, file_path)
        
        # Create extract variable refactoring
        extract = ExtractVariable(project, resource, start_offset, end_offset)
        
        # Get the changes
        changes = extract.get_changes(new_name)
        
        if preview:
            changed_files = []
            for change in changes.get_changed_resources():
                old_content = change.read()
                new_content = changes.get_changed_contents()[change]
                changed_files.append({
                    "file": str(change.path),
                    "oldContent": old_content,
                    "newContent": new_content
                })
            
            return {
                "success": True,
                "preview": True,
                "changedFiles": changed_files,
                "description": changes.description
            }
        else:
            project.do(changes)
            
            changed_files = [str(r.path) for r in changes.get_changed_resources()]
            
            return {
                "success": True,
                "preview": False,
                "changedFiles": changed_files,
                "description": changes.description
            }
    except Exception as e:
        return {
            "success": False,
            "error": str(e),
            "errorType": type(e).__name__
        }
    finally:
        if 'project' in locals():
            project.close()


def find_references(project_path: str, file_path: str, offset: int) -> dict[str, Any]:
    """
    Find all references to a symbol.
    
    Args:
        project_path: Root of the Python project
        file_path: Path to the file containing the symbol
        offset: Character offset of the symbol in the file
        
    Returns:
        Dictionary with success status and list of references
    """
    try:
        project = get_project(project_path)
        resource = get_resource(project, file_path)
        
        # Find occurrences using findit
        occurrences = findit.find_occurrences(project, resource, offset)
        
        references = []
        for occurrence in occurrences:
            references.append({
                "file": str(occurrence.resource.path),
                "offset": occurrence.offset,
                "isDefinition": occurrence.is_defined(),
                "isWrite": occurrence.is_written()
            })
        
        return {
            "success": True,
            "references": references,
            "count": len(references)
        }
    except Exception as e:
        return {
            "success": False,
            "error": str(e),
            "errorType": type(e).__name__
        }
    finally:
        if 'project' in locals():
            project.close()


def find_definition(project_path: str, file_path: str, offset: int) -> dict[str, Any]:
    """
    Find the definition of a symbol.
    
    Args:
        project_path: Root of the Python project
        file_path: Path to the file containing the symbol
        offset: Character offset of the symbol in the file
        
    Returns:
        Dictionary with success status and definition location
    """
    try:
        project = get_project(project_path)
        resource = get_resource(project, file_path)
        
        # Find definition using findit
        definition = findit.find_definition(project, resource, offset)
        
        if definition is None:
            return {
                "success": True,
                "found": False,
                "message": "Definition not found"
            }
        
        return {
            "success": True,
            "found": True,
            "file": str(definition.resource.path),
            "offset": definition.offset,
            "line": definition.lineno
        }
    except Exception as e:
        return {
            "success": False,
            "error": str(e),
            "errorType": type(e).__name__
        }
    finally:
        if 'project' in locals():
            project.close()


def main():
    parser = argparse.ArgumentParser(description="Python refactoring tool using rope")
    subparsers = parser.add_subparsers(dest="command", required=True)
    
    # Rename command
    rename_parser = subparsers.add_parser("rename", help="Rename a symbol")
    rename_parser.add_argument("--project", required=True, help="Project root path")
    rename_parser.add_argument("--file", required=True, help="File containing the symbol")
    rename_parser.add_argument("--offset", type=int, required=True, help="Character offset of the symbol")
    rename_parser.add_argument("--new-name", required=True, help="New name for the symbol")
    rename_parser.add_argument("--preview", action="store_true", help="Preview changes without applying")
    
    # Extract method command
    extract_method_parser = subparsers.add_parser("extract-method", help="Extract code into a method")
    extract_method_parser.add_argument("--project", required=True, help="Project root path")
    extract_method_parser.add_argument("--file", required=True, help="File containing the code")
    extract_method_parser.add_argument("--start", type=int, required=True, help="Start offset")
    extract_method_parser.add_argument("--end", type=int, required=True, help="End offset")
    extract_method_parser.add_argument("--new-name", required=True, help="Name for the new method")
    extract_method_parser.add_argument("--preview", action="store_true", help="Preview changes without applying")
    
    # Extract variable command
    extract_var_parser = subparsers.add_parser("extract-variable", help="Extract expression into a variable")
    extract_var_parser.add_argument("--project", required=True, help="Project root path")
    extract_var_parser.add_argument("--file", required=True, help="File containing the expression")
    extract_var_parser.add_argument("--start", type=int, required=True, help="Start offset")
    extract_var_parser.add_argument("--end", type=int, required=True, help="End offset")
    extract_var_parser.add_argument("--new-name", required=True, help="Name for the new variable")
    extract_var_parser.add_argument("--preview", action="store_true", help="Preview changes without applying")
    
    # Find references command
    refs_parser = subparsers.add_parser("find-references", help="Find all references to a symbol")
    refs_parser.add_argument("--project", required=True, help="Project root path")
    refs_parser.add_argument("--file", required=True, help="File containing the symbol")
    refs_parser.add_argument("--offset", type=int, required=True, help="Character offset of the symbol")
    
    # Find definition command
    def_parser = subparsers.add_parser("find-definition", help="Find the definition of a symbol")
    def_parser.add_argument("--project", required=True, help="Project root path")
    def_parser.add_argument("--file", required=True, help="File containing the symbol")
    def_parser.add_argument("--offset", type=int, required=True, help="Character offset of the symbol")
    
    args = parser.parse_args()
    
    if args.command == "rename":
        result = rename_symbol(args.project, args.file, args.offset, args.new_name, args.preview)
    elif args.command == "extract-method":
        result = extract_method(args.project, args.file, args.start, args.end, args.new_name, args.preview)
    elif args.command == "extract-variable":
        result = extract_variable(args.project, args.file, args.start, args.end, args.new_name, args.preview)
    elif args.command == "find-references":
        result = find_references(args.project, args.file, args.offset)
    elif args.command == "find-definition":
        result = find_definition(args.project, args.file, args.offset)
    else:
        result = {"success": False, "error": f"Unknown command: {args.command}"}
    
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
