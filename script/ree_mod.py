# RE engine modding library
# Uses reeutils for serialization / deserialization of pak files and REE files.

import json
import os
import shutil
import subprocess

def ensuredir(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)

def rmrf(path):
    if os.path.isdir(path):
        shutil.rmtree(path)
    elif os.path.isfile(path):
        os.remove(path)

def openjson(path):
    with open(path, 'r') as file:
        return json.load(file)

def savejson(path, data):
    with open(path, 'w') as file:
        json.dump(data, file, indent=4)
    
def writetext(path, text):
    with open(path, 'w') as file:
        file.write(text)

class ReeMod:
    """
    Represents a mod for an RE engine game where files can be extracted from vanilla .pak files,
    modified and then archived into a new .pak patch file or fluffy mod.
    """
    def __init__(self, name, author):
        self.debug = False
        self.name = name
        self.author = author
        self.version = "1.0"
        self.description = ""
        self.category = "!Other > Misc"
        self.work_dir = os.path.join(os.getcwd(), f".{name.lower()}_tmp")
        self.baseline = []
        self._create_work_folder()
        self._create_mod_ini()

    def __del__(self):
        if not self.debug:
            rmrf(self.work_dir)

    def _create_work_folder(self):
        rmrf(self.work_dir)
        os.makedirs(self.work_dir)

    def _create_mod_ini(self):
        lines = [
            f"name={self.name}",
            f"version={self.version}",
            f"description={self.description}",
            f"author={self.author}",
            f"category={self.category}",
            ""
        ]
        content = "\n".join(lines)
        with open(self._get_work_path("modinfo.ini"), 'w') as file:
            file.write(content)

    def _get_work_path(self, relative_path):
        return os.path.join(self.work_dir, relative_path)

    def save_pak(self, path):
        """
        Saves the mod as a pak file that can be placed in the installation directory
        of an REE game.
        """
        self._exec_tool(["pack", "-C", self.work_dir, "-o", path, self._get_work_path("natives")])

    def save_fluffy(self, path=None):
        """
        Saves the mod as a zip file compatible with fluffy mod manager.
        """
        if path is None:
            path = f"{self.name}.zip"
        if path.lower().endswith(".zip"):
            expected_path = f"{path}.zip"
            rmrf(expected_path)
            rmrf(path)
            shutil.make_archive(path, 'zip', self.work_dir)
            os.rename(expected_path, path)
        else:
            rmrf(path)
            shutil.copytree(self.work_dir, path)

    def set_baseline(self, baseline):
        """
        Sets the base line PAK files and directories for retrieving the original
        file. The first file found in descending order is chosen.
        """
        self.baseline = baseline

    def export(self, path, output):
        """
        Deserializes an REE file into a JSON file.
        """
        self._exec_tool_default(["export", path, "-o", output])

    def import_file(self, path, input):
        """
        Copies an existing file into the mod at the given path.
        """
        shutil.copyfile(input, self._get_and_ensure_mod_path(path))

    def import_json(self, path, input):
        """
        Serializes a JSON file into an REE file for the mod at the given path.
        """
        modpath = self._get_and_ensure_mod_path(path)
        self._exec_tool(["import", "-o", modpath, input])

    def modify(self, path, cb):
        """
        Modifies the latest version of the given REE file by deserializing it,
        passing it to the callback for edits, then serializes back into
        a REE file for the mod.
        """
        has_extension = lambda ext: path.lower().endswith(ext)
        if has_extension('.scn.2'):
            raise Exception("Scenes not yet supported")
        elif has_extension('.user.2') or has_extension('.msg.22'):
            data = self._get_json(path)
            cb(data)
            self._set_json(path, data)
        else:
            raise Exception("Unsupported file type")

    def _get_json(self, path):
        tmp_path = self._get_work_path("tmp.json")
        self.export(path, tmp_path)
        data = openjson(tmp_path)
        rmrf(tmp_path)
        return data

    def _set_json(self, path, data):
        tmp_path = self._get_work_path("tmp.json")
        savejson(tmp_path, data)
        self.import_json(path, tmp_path)
        rmrf(tmp_path)

    def _get_and_ensure_mod_path(self, path):
        modpath = self._get_work_path(path)
        ensuredir(modpath)
        return modpath

    def _create_config(self):
        config_path = self._get_work_path("input.json")
        data = {
            "baseline": self.baseline + [self.work_dir],
            "command": "dump"
        }
        with open(config_path, 'w') as json_file:
            json.dump(data, json_file, indent=4)

    def _exec_tool_default(self, args):
        final_args = args
        for bl in self.baseline:
            final_args += ["-I", bl]
        final_args += ["-I", self.work_dir]
        self._exec_tool(final_args)

    def _exec_tool(self, args):
        command = ["reeutils"] + args
        # print(command)
        result = subprocess.run(command, capture_output=True)
        if result.returncode != 0:
            raise Exception(f"reeutils failed with exit code {result.returncode}")
