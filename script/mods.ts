import { ReeMod } from "./ree_mod.ts";

export class ModsContext {
    private mod?: ReeMod;

    author = "BioRand Team";
    baseLine: string[] = [];

    async createSingleMod(name: string, f: () => Promise<void>) {
        await this.createMod(name, async g => {
            this.mod = g;
            await f();
        });
    }

    async createOrApplyMod(name: string, f: (mod: ReeMod) => Promise<void>) {
        if (this.mod) {
            await f(this.mod);
            console.log(`Applied ${name}`);
        } else {
            await this.createMod(name, async g => {
                await f(g);
            });
        }
    }

    private async createMod(name: string, f: (mod: ReeMod) => Promise<void>) {
        const zipName = `mods/${name}.zip`;
        const mod = await ReeMod.create(name, this.author);
        mod.setBaseline(this.baseLine);
        await f(mod);
        await mod.saveFluffy(zipName);
        await mod.dispose();
        console.log(`Created ${zipName}`);
    }
}
