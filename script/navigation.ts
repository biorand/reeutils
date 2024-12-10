import { ModsContext } from "./mods.ts";
import { GameObject, Scene } from "./ree_mod.ts";

function iterateGameObjects(scn: Scene) {
    const result: GameObject[] = [];
    const iter = (parent: Scene) => {
        for (const child of parent) {
            if (child.$type == "via.GameObject") {
                result.push(child);
            }
            if (child.children) {
                iter(child.children);
            }
        }
    };
    iter(scn);
    return result;
}

export async function createNovisNavigation(ctx: ModsContext) {
    const groups = [
        {
            path: 'natives/stm/_chainsaw/appsystem/navigation/loc{0}/navigation_loc{1}.scn.20',
            rootName: 'AIMap',
            locations: [
                4000, 4010, 4011, 4300, 4310, 4400, 4410, 4500, 4510, 4600, 4610, 4700, 4710,
                5000, 5010, 5100, 5110, 5200, 5300, 5400, 5410, 5500, 5510, 5600, 5610, 5700, 5900,
                6000, 6010, 6100, 6110, 6200, 6300, 6400, 6500, 6600, 6610, 6700, 6701, 6800, 6801, 6900,
            ]
        },
        {
            path: 'natives/stm/_anotherorder/appsystem/navigation/loc{0}/navigation_loc{1}.scn.20',
            rootName: 'AIMap_AO',
            locations: [
                4010, 4300, 4400, 4410, 4500, 4700,
                5000, 5100, 5110, 5500, 5510, 5600, 5610, 5900,
                6010, 6100, 6110, 6200, 6400, 6800, 6900,
            ]
        }
    ];

    await ctx.createOrApplyMod("novis_navigation", async mod => {
        for (const g of groups) {
            for (const loc of g.locations) {
                const st00 = `${~~(loc / 100)}`;
                const st0000 = `${loc}`;
                const path = g.path
                    .replace("{0}", st00)
                    .replace("{1}", st0000);
                await mod.modify(path, f => {
                    const gameObject = iterateGameObjects(f).find(x => x.v0 == g.rootName);
                    if (gameObject) {
                        const navigationMapClient = gameObject.components?.find(x => x.$type == "chainsaw.NavigationMapClient");
                        const bindInfoList = navigationMapClient._BindInfoList;
                        if (bindInfoList.length < 3) {
                            bindInfoList.push({
                                "$type": "chainsaw.NavigationMapClient.BindInfo",
                                _Purpose: 1,
                                _MapName: `VolumeSpace_Loc${loc}`
                            });
                        } else {
                            // Do not edit file
                            return false;
                        }
                    }
                });
            }
        }
    });
}
