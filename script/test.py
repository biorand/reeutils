import os
from ree_mod import ReeMod

if __name__ == "__main__":
    vanilla_path = "G:\\re4r\\vanilla"
    vanilla_files = [
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
    ]
    vanilla_paths = [os.path.join(vanilla_path, p) for p in vanilla_files]

    mod = ReeMod("funnystrings", "IntelOrca")
    mod.set_baseline(vanilla_paths)

    def set_msg_en(msg, guid, value):
        for entry in msg["entries"]:
            if entry["guid"] == guid:
                entry["values"][1] = value
                break

    def chg(msg):
        set_msg_en(msg, "6766b978-b6cb-42fe-91ca-ce55edabcea9", "A very green herb that\r\nrestores plenty of health.")
    mod.modify("natives/stm/_chainsaw/message/mes_main_item/ch_mes_main_item_caption.msg.22", chg)

    mod.save_fluffy()
    mod.save_pak("re_chunk_000.pak.patch_004.pak")
