import { ModsContext } from "./mods.ts";

export async function createQuickBuy(ctx: ModsContext) {
    const time = 0; // default is 0.6
    await ctx.createOrApplyMod("quick_buy", async mod => {
        await mod.modify("natives/stm/_chainsaw/appsystem/ui/userdata/guiparamholdersettinguserdata.user.2", f => {
            f._InGameShopGuiParamHolder._HoldTime_Purchase = time;
        });
    });
}

export async function createIncreasedJetSkiTimer(ctx: ModsContext) {
    const timerNumSeconds = 7 * 60;
    await ctx.createOrApplyMod("long_jet_ski_timer", async mod => {
        await mod.modify("natives/stm/_chainsaw/appsystem/ui/userdata/guiparamholdersettinguserdata.user.2", f => {
            const timerSettings = f._TimerGuiParamHolder._TimerParamSettings[0];
            timerSettings._MaxSecond = timerNumSeconds;
            timerSettings._RespawnTimer = timerNumSeconds;
            for (const d of [10, 20, 30, 40]) {
                const sub = timerSettings[`_TimerParam_Defficulty${d}`];
                sub.MaxSecond = timerNumSeconds;
                sub.RespawnTimer = timerNumSeconds;
            }
        });
    });
}
