import { ModsContext } from "./mods.ts";
import { MsgFile, ReeMod } from "./ree_mod.ts";
import * as path from "jsr:@std/path";
import { createLaserSightMod } from "./weapons.ts";

function setMessageEn(msg: MsgFile, guid: string, value: string) {
    for (const entry of msg.entries) {
        if (entry.guid == guid) {
            entry.values[1] = value;
            break;
        }
    }
}

async function main() {
    const ctx = new ModsContext();
    ctx.baseLine = getVanillaPaths();
    await createLaserSightMod(ctx);

    // const mod = await ReeMod.create("funnystrings", "IntelOrca");
    // try {
    //     mod.setBaseline(getVanillaPaths());
    //     await mod.modify("natives/stm/_chainsaw/message/mes_main_item/ch_mes_main_item_caption.msg.22", msg => {
    //         setMessageEn(msg, "6766b978-b6cb-42fe-91ca-ce55edabcea9", "A very green herb that\r\nrestores plenty of health.");
    //     });
    //     await mod.saveFluffy("funnystrings.zip");
    //     await mod.savePak("re_chunk_000.pak.patch_004.pak");
    // } finally {
    //     await mod.dispose();
    // }
}

function getVanillaPaths() {
    const vanillaPath = "D:\\SteamLibrary\\steamapps\\common\\RESIDENT EVIL 4  BIOHAZARD RE4";
    const vanillaFiles = [
        "dlc/re_dlc_stm_2109300.pak",
        "dlc/re_dlc_stm_2109301.pak",
        "dlc/re_dlc_stm_2109303.pak",
        "dlc/re_dlc_stm_2109304.pak",
        "dlc/re_dlc_stm_2109305.pak",
        "dlc/re_dlc_stm_2109306.pak",
        "dlc/re_dlc_stm_2109307.pak",
        "dlc/re_dlc_stm_2109308.pak",
        "dlc/re_dlc_stm_2109309.pak",
        "dlc/re_dlc_stm_2109310.pak",
        "dlc/re_dlc_stm_2109311.pak",
        "dlc/re_dlc_stm_2109312.pak",
        "dlc/re_dlc_stm_2109313.pak",
        "dlc/re_dlc_stm_2109314.pak",
        "dlc/re_dlc_stm_2109315.pak",
        "re_chunk_000.pak",
        "re_chunk_000.pak.patch_001.pak",
        "re_chunk_000.pak.patch_002.pak",
        "re_chunk_000.pak.patch_003.pak"
    ];
    return vanillaFiles.map(x => path.join(vanillaPath, x));
}

await main();
