import { ReeMod } from "./ree_mod.ts";

export class ModsContext {
    author = "BioRand Team";
    baseLine: string[] = [];

    async createOrApplyMod(name: string, f: (mod: ReeMod) => Promise<void>) {
        const mod = await ReeMod.create(name, this.author);
        mod.setBaseline(this.baseLine);
        await f(mod);
        await mod.saveFluffy(`mods/${name}.zip`);
        await mod.dispose();
    }
}
