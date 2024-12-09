import { ModsContext } from "./mods.ts";

const ItemIds = {
    AT_LASER_SIGHT: 116008000,
    WP_RED9: 274838656,
    WP_BLACKTAIL: 274840256,
    WP_MATILDA: 274841856,
    WP_SW_BLACKTAIL: 278200256,
    WP_SW_RED9: 278216256,
};

const WeaponIds = {
    WP_SG09: 4000,
    WP_RED9: 4002,
    WP_BLACKTAIL: 4003,
    WP_MATILDA: 4004,
    WP_SW_BLACKTAIL: 6103,
    WP_SW_RED9: 6113
};

export async function createLaserSightMod(ctx: ModsContext) {
    await ctx.createOrApplyMod("lasersight", async mod => {
        // Weapon part combine
        await mod.modify("natives/stm/_chainsaw/appsystem/ui/userdata/weaponpartscombinedefinitionuserdata.user.2", f => {
            const laserSight = f._Datas.find(x => x._ItemId == ItemIds.AT_LASER_SIGHT);
            laserSight._TargetItemIds.push(ItemIds.WP_RED9);
            laserSight._TargetItemIds.push(ItemIds.WP_BLACKTAIL);
            laserSight._TargetItemIds.push(ItemIds.WP_MATILDA);
        });
        await mod.modify("natives/stm/_anotherorder/appsystem/ui/userdata/weaponpartscombinedefinitionuserdata_ao.user.2", f => {
            const laserSight = f._Datas.find(x => x._ItemId == ItemIds.AT_LASER_SIGHT);
            laserSight._TargetItemIds.push(ItemIds.WP_SW_BLACKTAIL);
            laserSight._TargetItemIds.push(ItemIds.WP_SW_RED9);
        });

        // Weapon details
        await mod.modify("natives/stm/_chainsaw/appsystem/weaponcustom/weapondetailcustomuserdata.user.2", f => {
            const laserSight = f._WeaponDetailStages
                .flatMap(x => x._WeaponDetailCustom._AttachmentCustoms)
                .find(x => x._ItemID == ItemIds.AT_LASER_SIGHT);
            for (const wp of [WeaponIds.WP_RED9, WeaponIds.WP_BLACKTAIL, WeaponIds.WP_MATILDA]) {
                const wpDetail = f._WeaponDetailStages.find(x => x._WeaponID == wp);
                wpDetail._WeaponDetailCustom._AttachmentCustoms.push(laserSight);
            }
        });
        await mod.modify("natives/stm/_anotherorder/appsystem/weaponcustom/weapondetailcustomuserdata_ao.user.2", f => {
            const laserSight = f._WeaponDetailStages
                .flatMap(x => x._WeaponDetailCustom._AttachmentCustoms)
                .find(x => x._ItemID == ItemIds.AT_LASER_SIGHT);
            for (const wp of [WeaponIds.WP_SW_BLACKTAIL, WeaponIds.WP_SW_RED9]) {
                const wpDetail = f._WeaponDetailStages.find(x => x._WeaponID == wp);
                wpDetail._WeaponDetailCustom._AttachmentCustoms.push(laserSight);
            }
        });

        // Laser sight
        await mod.modify("natives/stm/_chainsaw/appsystem/weapon/lasersight/playerlasersightcontrolleruserdata.user.2", f => {
            const wps = [
                WeaponIds.WP_RED9,
                WeaponIds.WP_BLACKTAIL,
                WeaponIds.WP_MATILDA,
                WeaponIds.WP_SW_BLACKTAIL,
                WeaponIds.WP_SW_RED9
            ];
            for (const wp of wps) {
                f._Settings.push({...f._Settings[0], _WeaponID: wp });
            }
        });
    });
}
