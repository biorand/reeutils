// RE engine modding library
// Uses reeutils for serialization / deserialization of pak files and REE files.

// deno-lint-ignore-file no-explicit-any
import process from "node:process";
import * as fs from "jsr:@std/fs";
import * as path from "jsr:@std/path";

export interface MsgFile {
    entries: MsgFileEntry[];
}

export interface MsgFileEntry {
    guid: string;
    values: string[];
}

export interface Folder {
    "$type": "via.Folder",
    v0: string;
    v1: string;
    v2: number;
    v3: number;
    v4: number;
    v5: string;
    v6: string;
    children?: (Folder | GameObject)[];
}

export interface GameObject {
    "$type": "via.GameObject",
    v0: string;
    v1: string;
    v2: number;
    v3: number;
    v4: number;
    guid: string;
    prefab?: string;
    components?: any[];
    children?: GameObject[];
}

export type Scene = (Folder | GameObject)[];

async function mkdir(path: string) {
    await fs.ensureDir(path);
}

async function ensuredir(p: string) {
    const dir = path.dirname(p);
    await mkdir(dir);
}

async function rmrf(root: string) {
    if (await fs.exists(root)) {
        await Deno.remove(root, { recursive: true });
    }
}

async function cpr(src: string, dest: string) {
    await fs.copy(src, dest);
}

async function zip(outputPath: string, dir: string) {
    await rmrf(outputPath);
    await ensuredir(outputPath);
    const path7zip = "C:\\Program Files\\7-Zip\\7z.exe";
    const cmd = new Deno.Command(path7zip, {
        args: ["a", "-r", path.resolve(outputPath), "."],
        cwd: dir
    });
    const output = await cmd.output();
    if (output.code != 0) {
        throw new Error(`Failed to archive ${dir}`);
    }
}

async function readJsonFile(path: string) {
    const content = await Deno.readTextFile(path)
    return JSON.parse(content);
}

async function writeFile(path: string, content: string) {
    await Deno.writeTextFile(path, content);
}

async function writeJsonFile(path: string, content: any) {
    await Deno.writeTextFile(path, JSON.stringify(content));
}

export class ReeMod {
    debug = false;
    name: string;
    author: string;
    version: string;
    description: string;
    category: string;
    private workDir: string;
    private baseline: string[];

    private constructor(name: string, author: string) {
        this.debug = false;
        this.name = name;
        this.author = author;
        this.version = "1.0";
        this.description = "";
        this.category = "!Other > Misc";
        this.workDir = path.join(process.cwd(), `.${name.toLowerCase()}_tmp`);
        this.baseline = [];
    }

    static async create(name: string, author: string) {
        const result = new ReeMod(name, author);
        await result.createWorkFolder();
        await result.createModIni();
        new FinalizationRegistry(_ => {
            result.dispose();
        }).register(result, null);
        return result;
    }

    async dispose() {
        if (!this.debug) {
            await rmrf(this.workDir);
        }
    }

    async createWorkFolder() {
        await rmrf(this.workDir)
        await mkdir(this.workDir)
    }

    private getWorkPath(relativePath: string) {
        return path.join(this.workDir, relativePath);
    }

    private getAndEnsureModPath(path: string){
        const modpath = this.getWorkPath(path);
        ensuredir(modpath);
        return modpath;
    }

    async createModIni() {
        const lines = [
            `name=${this.name}`,
            `version=${this.version}`,
            `description=${this.description}`,
            `author=${this.author}`,
            `category=${this.category}`,
            ""
        ];
        const content = lines.join("\n");
        await writeFile(this.getWorkPath("modinfo.ini"), content);
    }

    /**
     * Saves the mod as a pak file that can be placed in the installation directory
     * of an REE game.
     * @param path
     */
    async savePak(path: string) {
        await this.execTool(["pack", "-C", this.workDir, "-o", path, this.getWorkPath("natives")]);
    }

    /**
     * Saves the mod as a zip file compatible with fluffy mod manager.
     * @param path
     */
    async saveFluffy(path: string) {
        if (!path)
            path = `${this.name}.zip`;
        if (path.toLowerCase().endsWith(".zip")) {
            await zip(path, this.workDir);
        } else {
            await rmrf(path);
            await cpr(this.workDir, path);
        }
    }

    /**
     * Sets the base line PAK files and directories for retrieving the original
     * file. The first file found in descending order is chosen.
     * @param self
     * @param baseline
     */
    setBaseline(baseline: string[]) {
        this.baseline = baseline;
    }

    /**
     * Deserializes an REE file into a JSON file.
     * @param path
     * @param output
     */
    async export(path: string, output: string) {
        await this.execToolDefault(["export", path, "-o", output]);
    }

    /**
     * Copies an existing file into the mod at the given path.
     * @param path 
     * @param input 
     */
    async importFile(path: string, input: string) {
        await cpr(input, this.getAndEnsureModPath(path));
    }

    /**
     * Serializes a JSON file into an REE file for the mod at the given path.
     * @param path 
     * @param input 
     */
    async importJson(path: string, input: string) {
        const modpath = this.getAndEnsureModPath(path);
        await this.execTool(["import", "-g", "re4", "-o", modpath, input]);
    }

    /**
     * Modifies the latest version of the given REE file by deserializing it,
     * passing it to the callback for edits, then serializes back into
     * a REE file for the mod.
     * @param path 
     * @param cb 
     */
    async modify(path: string, cb: (f: MsgFile | any) => false | undefined) {
        const hasExtension = (ext: string) => path.toLowerCase().endsWith(ext)
        if (hasExtension('.scn.20') || hasExtension('.user.2') || hasExtension('.msg.22')) {
            const data = await this.getJson(path);
            if (cb(data) !== false) {
                await this.setJson(path, data);
                console.log(`  modify ${path}`);
            }
        } else {
            throw new Error("Unsupported file type");
        }
    }

    private async getJson(path: string) {
        const tmpPath = this.getTempJsonFileName();
        await this.export(path, tmpPath)
        const data = await readJsonFile(tmpPath)
        await rmrf(tmpPath)
        return data
    }

    private async setJson(path: string, data: any) {
        const tmpPath = this.getTempJsonFileName();
        await writeJsonFile(tmpPath, data)
        await this.importJson(path, tmpPath)
        await rmrf(tmpPath)
    }

    private getTempJsonFileName() {
        const letters = "0123456789abcdefghijklmnopqrstuvwxyz";
        let fileName = "tmp_";
        for (let i = 0; i < 5; i++) {
            fileName += letters[~~(Math.random() * letters.length)];
        }
        fileName += ".json";
        return this.getWorkPath(fileName);
    }

    private async execToolDefault(args: string[]) {
        let finalArgs = args;
        finalArgs = [...finalArgs, "-g", "re4"];
        for (const bl of this.baseline) {
            finalArgs = [...finalArgs, "-I", bl];
        }
        finalArgs = [...finalArgs, "-I", this.workDir];
        await this.execTool(finalArgs);
    }

    private async execTool(args: string[]) {
        const command = new Deno.Command("reeutils", {
            args
        });
        // console.log(`reeutils ${args.join(" ")}`);
        const result = await command.output();
        switch (result.code) {
            case 0:
                break;
            case 2:
                throw new Error(new TextDecoder().decode(result.stderr));
            default:
                console.error("Command was:")
                console.error(`    reeutils ${args.join(" ")}`);
                console.error("stdout/stderr:")
                console.error(new TextDecoder().decode(result.stdout));
                console.error(new TextDecoder().decode(result.stderr));
                throw new Error(`reeutils failed with exit code ${result.code}`);
        }
    }
}
