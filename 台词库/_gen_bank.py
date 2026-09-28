#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""生成《港口宝石失窃案》台词库骨架 JSON。text 一律留白，summary 为简略描述。"""
import json, collections, os

OUT = "/Users/ouyang2005/Documents/qwen-agent/XZs3TnbjzR/default/台词库.json"

# ---------------------------------------------------------------- 枚举字典
NPCS = [
    ("young_noble",    "NOB", "quest_giver", "年轻贵族",   25, ["谨慎", "简短", "克制", "体弱"], None,
     ["wharf_12", "mansion_study", "court", "court_corridor"], ["reserved", "trusting", "revealed"]),
    ("detective",      "DET", "ally",        "警探",       32, ["务实", "句首口癖", "可靠"], "嗯",
     ["wharf_12", "police_station", "court"], ["suspicious", "neutral", "trusting"]),
    ("dock_manager",   "MGR", "antagonist",  "码头经理",   38, ["回避", "含糊", "后期崩溃"], None,
     ["wharf_12", "manager_office", "cold_storage", "police_station", "court"],
     ["present", "evasive", "fleeing", "arrested", "confessed"]),
    ("lawyer",         "LAW", "antagonist",  "律师",       52, ["程序化", "否认", "精确", "冷静"], None,
     ["lawyer_office", "court", "court_corridor"], ["absent", "composed", "cornered", "convicted"]),
    ("landlady",       "LND", "informant",   "破锚酒馆老板娘", None, ["市井", "直接", "讲条件", "戒备"], None,
     ["tavern"], ["wary", "cooperative"]),
    ("editor",         "EDT", "hidden_line", "报社主编",   None, ["语气如常", "话少", "岔开话题", "不解释"], None,
     ["newsroom"], ["composed", "guarded", "exposed"]),
    ("manager_wife",   "WIF", "witness",     "码头经理妻子", None, ["紧张", "多以书信出现"], None,
     ["hotel_swan", "court"], ["hidden", "fled", "testified"]),
    ("lawyer_clerk",   "CLK", "witness",     "律师秘书",   None, ["低声", "不能说多"], None,
     ["lawyer_office"], ["guarded", "leaking"]),
    ("dock_worker",    "WKR", "witness",     "码头工人",   None, ["口语", "只讲看见的"], None,
     ["wharf_12", "wharf_alley"], ["present"]),
    ("customs_officer","CUS", "functional",  "海关职员",   None, ["公事公办", "按规程"], None,
     ["customs_house"], ["present"]),
    ("police_officer", "OFC", "functional",  "警员",       None, ["报告式"], None,
     ["police_station", "kent_attic"], ["present"]),
    ("judge",          "JDG", "functional",  "法官",       None, ["格式化法律用语"], None,
     ["court"], ["present"]),
    ("old_butler",     "BTL", "witness",     "贵族家老管家", None, ["老年", "守礼"], None,
     ["customs_house", "mansion_study"], ["offscreen"]),
    ("narrator",       "NAR", "narrator",    "旁白／记者内心", None, ["第一人称观察"], None,
     [], []),
]

LOCS = [
    ("wharf_12",        "WHARF",   "12号码头仓库",              "案发现场，搜证核心"),
    ("wharf_alley",     "ALLEY",   "码头后巷",                  "马车痕迹"),
    ("cold_storage",    "COLD",    "废弃冷藏库",                "F3前哨，隧道入口"),
    ("tunnel",          "TUNNEL",  "地下隧道",                  "空木盒与发缕"),
    ("tavern",          "TAVERN",  "破锚酒馆",                  "情报交换（F1）"),
    ("mansion_study",   "MANSION", "贵族宅邸书房·梅菲尔",        "委托会面（F2）、身世线索"),
    ("newsroom",        "NEWS",    "报社",                      "主编暗线"),
    ("police_station",  "POLICE",  "码头警局",                  "警探情报、结案"),
    ("customs_house",   "CUSTOMS", "海关大楼",                  "报关单、多佛底单"),
    ("manager_office",  "MGOFF",   "码头经理办公室",            "短函、台历、钥匙"),
    ("lawyer_office",   "LAWOFF",  "律师办公室",                "律师缺席、秘书"),
    ("hotel_swan",      "HOTEL",   "天鹅与皇冠旅馆",            "妻子的信"),
    ("park_station",    "STATION", "皇后公园站储物柜",          "追回钻石"),
    ("court",           "COURT",   "法庭",                      "最终对峙（F3）"),
    ("court_corridor",  "CORRIDOR","法庭走廊",                  "庭后低声质问"),
    ("kent_attic",      "KENT",    "肯特郡七橡树镇磨坊巷3号阁楼","尾声／续作钩子"),
]

EVENTS = [
    ("prologue_wharf",     1, "wharf_12",      False),
    ("D1_action1_search",  1, "wharf_12",      False),
    ("D1_action2_news",    1, "newsroom",      False),
    ("D1_action3_night",   1, "wharf_12",      False),
    ("F1_tavern_reveal",    1, "tavern",        True),
    ("D2_action_customs",  2, "customs_house", False),
    ("D2_action_tavern",   2, "tavern",        False),
    ("D2_action_police",   2, "police_station",False),
    ("D2_action_wharf",    2, "wharf_12",      False),
    ("D3_action_mgoff",    3, "manager_office",False),
    ("D3_action_recheck",  3, "wharf_12",      False),
    ("D3_action_dover",    3, "customs_house", False),
    ("D3_action_news",     3, "newsroom",      False),
    ("D4_action_lawoff",   4, "lawyer_office", False),
    ("D4_action_tavern",   4, "tavern",        False),
    ("F2_mansion_photo",   4, "mansion_study", True),
    ("D5_action_hotel",    5, "hotel_swan",    False),
    ("D5_action_police",   5, "police_station",False),
    ("D5_action_tunnel",   5, "tunnel",        False),
    ("D6_action_station",  6, "park_station",  False),
    ("D6_action_return",   6, "mansion_study", False),
    ("D6_action_news",     6, "newsroom",      False),
    ("F3_court",           7, "court",         True),
    ("D7_action_police",   7, "police_station",False),
    ("D7_action_final",    7, "newsroom",      False),
]

FLAGS = [
    ("main.case_opened",            "main",   "案件正式启动，可开始搜证"),
    ("main.found_pink_shards",      "main",   "夹层内发现粉红色晶屑"),
    ("main.found_drag_marks",       "main",   "地面拖痕指向冷藏库"),
    ("main.worker_testimony",       "main",   "获得工人关于十点半木箱的口供"),
    ("main.registry_gap",           "main",   "登记簿转运签收栏空白"),
    ("main.know_hidden_layer",      "main",   "得知货箱有夹层（F1）"),
    ("main.know_dover_route",       "main",   "得知箱子经多佛、老管家经手"),
    ("main.customs_unvalued",       "main",   "报关单上的不计价私人信物"),
    ("main.anon_note",              "main",   "获得匿名纸条，指向皇后公园站"),
    ("main.absence_pattern",        "main",   "经理四个晚上签到未签离岗"),
    ("main.black_carriage",         "main",   "黑色马车停后巷半小时，往内城"),
    ("main.typed_memo",             "main",   "获得打字机短函（优先取出粉红物件）"),
    ("main.calendar_n_o",           "main",   "台历5月18日圈两圈并注N.O."),
    ("main.dover_marginalia",       "main",   "多佛底单老管家手写备注（红绸圆盒）"),
    ("main.lawyer_wapping",         "main",   "查出律师上周赴Wapping出差三天"),
    ("main.wife_fled",              "main",   "得知经理妻子凌晨携皮箱投旅馆"),
    ("main.diamond_identity",       "main",   "得知钻石名为东方之星且为信托凭证"),
    ("main.branch_motive",          "main",   "得知旁支争夺信托基金归属"),
    ("main.seal_ring",              "main",   "获得银质封印戒指与授信函"),
    ("main.wife_letter",            "main",   "获得经理妻子的信，供出律师与八十镑"),
    ("main.know_kent_address",      "main",   "得知信函藏于肯特郡七橡树镇磨坊巷3号"),
    ("main.manager_used",           "main",   "警探确认经理系被利用"),
    ("main.empty_box",              "main",   "在隧道找到与压痕吻合的空木盒"),
    ("main.diamond_recovered",      "main",   "从17号储物柜追回东方之星"),
    ("main.veil_woman",             "main",   "得知雇主是戴面纱的女性"),
    ("main.ex_wife_identity",       "main",   "查明律师前妻改嫁入旁支、曾为府中婢女"),
    ("main.twelve_letters",         "main",   "获得律师与旁支十二封通信（定罪铁证）"),
    ("main.manager_confessed",      "main",   "码头经理认罪"),
    ("main.lawyer_convicted",       "main",   "判决结果：驳回辩方主张，准予羁押"),
    ("main.lawyer_bailed",          "main",   "判决结果：保释获准、限制出境（剧本原结局）"),
    ("main.case_closed",            "main",   "案件结案"),
    ("side.tunnel_found",           "side",   "发现废弃货架后的向下隧道入口"),
    ("side.cold_storage_key",       "side",   "获得经理办公室钥匙"),
    ("side.midnight_note",          "side",   "旧门房纸条：明日午夜老地方"),
    ("side.ink_trace",              "side",   "木板边缘被擦掉的墨迹残痕"),
    ("side.letter_half",            "side",   "主编桌上写给律师的半成品信"),
    ("side.lawyer_seen_tavern",      "side",   "老板娘证实律师曾常来酒馆"),
    ("hidden.photo_acquired",       "hidden", "获得旧银版照片，身世线激活"),
    ("hidden.face_resemblance",     "hidden", "认出照片女子与自己面容八九分相似"),
    ("hidden.hair_sample",          "hidden", "获得盒盖缝隙中的深棕色卷曲发缕"),
    ("hidden.mother_named",         "hidden", "确认生母为艾琳·哈珀，1882-83任侍女"),
    ("hidden.birth_year_gap",       "hidden", "记录载1884年生男婴，而记者生于1885年"),
    ("hidden.editor_bribe",         "hidden", "取得主编与律师资金往来证据（已付清·R）"),
    ("hidden.editor_aware",         "hidden", "主编察觉记者在追查自己"),
    ("hidden.wife_protected",       "hidden", "记者承诺保护经理妻子且未写她"),
    ("hidden.wife_testified",       "hidden", "支线：经理妻子答应出庭作证"),
]

ITEMS = [
    ("pink_shard",          "粉红色晶屑",       "evidence",   "DOC_WHARF_D1_002",  "critical"),
    ("red_velvet",          "深红锦缎内衬",     "evidence",   None,                "supporting"),
    ("red_fiber",           "暗红色织物纤维",   "evidence",   None,                "supporting"),
    ("registry_page",       "登记簿M-412页",    "document",   "DOC_WHARF_D1_002",  "critical"),
    ("midnight_note",       "旧门房新纸条",     "document",   "DOC_WHARF_D1_001",  "supporting"),
    ("customs_declaration", "报关单",           "document",   "DOC_CUSTOMS_D2_004","supporting"),
    ("anon_note",           "匿名纸条",         "document",   "DOC_TAVERN_D2_003", "critical"),
    ("typed_memo",          "打字机短函",       "document",   "DOC_MGOFF_D3_005",  "critical"),
    ("desk_calendar",       "办公室台历",       "document",   "DOC_MGOFF_D3_006",  "supporting"),
    ("ink_trace_board",     "墨迹残痕木板",     "evidence",   "DOC_MGOFF_D3_007",  "background"),
    ("half_written_letter", "主编的半成品信",   "document",   "DOC_NEWS_D3_009",   "supporting"),
    ("office_key",          "经理办公室钥匙",   "key_item",   None,                "critical"),
    ("dover_copy",          "多佛底单",         "document",   "DOC_CUSTOMS_D3_008","supporting"),
    ("diamond_archive",     "钻石档案",         "document",   "DOC_MANSION_D4_010","critical"),
    ("seal_ring",           "银质封印戒指",     "credential", "DOC_MANSION_D4_011","supporting"),
    ("credit_letter",       "授信函",           "credential", "DOC_MANSION_D4_011","supporting"),
    ("old_silver_photo",    "旧银版照片",       "personal",   "DOC_MANSION_D4_012","critical"),
    ("wife_letter",         "经理妻子的信",     "document",   "DOC_WIF_HOTEL_D5_002","critical"),
    ("empty_wood_box",      "空木盒",           "evidence",   "DOC_TUNNEL_D5_014", "critical"),
    ("brown_hair_lock",     "深棕色发缕",       "evidence",   "DOC_TUNNEL_D5_015", "critical"),
    ("eastern_star",        "东方之星粉钻",     "key_item",   None,                "critical"),
    ("maid_record",         "侍女雇佣记录",     "document",   "DOC_MANSION_D6_016","critical"),
    ("paid_note",           "已付清字条",       "document",   "DOC_NEWS_D6_017",   "critical"),
    ("iron_box_letters",    "铁盒与十二封信",   "document",   "DOC_POLICE_D7_018", "critical"),
    ("final_manuscript",    "最终报道稿",       "document",   None,                "background"),
]

# ---------------------------------------------------------------- 台词条目
# (id, npc, location, day, slot, visit, event, trigger_type, group, priority,
#  once, requires, forbids, req_items, rel, chain, summary, effects, next, tags)
L = []
def line(id, npc, loc, day, slot, trigger, summary, *, visit=None, event=None,
         prio=60, group=None, once=True, req=(), forb=(), items=(), rel=None,
         chain=None, eff=None, nxt=None, tags=(), typ="dialogue", fallback=None,
         emotion=None, tone=None, action=None, render="speech_bubble",
         target="player", action_budget=None, weather=None, item=None):
    L.append(dict(id=id, npc=npc, loc=loc, day=day, slot=slot, visit=visit, event=event,
                  trigger=trigger, summary=summary, prio=prio, group=group, once=once,
                  req=list(req), forb=list(forb), items=list(items), rel=rel or {},
                  chain=chain, eff=eff or {}, nxt=nxt, tags=list(tags), typ=typ,
                  fallback=fallback, emotion=emotion, tone=tone, action=action,
                  render=render, target=target, budget=action_budget, weather=weather,
                  item=item))

# ===== 8.1 年轻贵族 (42) =====
line("DL_NOB_WHARF_D1_001","young_noble","wharf_12",1,"dawn","enter_scene",
     "雾中站在货箱不远处沉默观察，不主动开口",event="prologue_wharf",prio=80,tags=["main","prologue"])
line("DL_NOB_WHARF_D1_002","young_noble","wharf_12",1,"dawn","choice_response",
     "承认自己看了半小时才注意到锁扣压痕",event="prologue_wharf",prio=60,group="nob_d1_reply",tags=["main"])
line("DL_NOB_WHARF_D1_003","young_noble","wharf_12",1,"dawn","choice_response",
     "察觉被打量穿着，反过来简短询问对方身份",event="prologue_wharf",prio=60,group="nob_d1_reply",tags=["main"])
line("DL_NOB_WHARF_D1_004","young_noble","wharf_12",1,"dawn","choice_response",
     "听完记者身份后，说明这批货是自己的",event="prologue_wharf",prio=60,group="nob_d1_reply",tags=["main"])
line("DL_NOB_WHARF_D1_005","young_noble","wharf_12",1,"dawn","choice_response",
     "玩家沉默时他先开口，语气略带试探",event="prologue_wharf",prio=60,group="nob_d1_reply",tags=["main"])
line("DL_NOB_WHARF_D1_006","young_noble","wharf_12",1,"dawn","forced_node",
     "给出联络方式，嘱有发现可告知码头管理处",event="prologue_wharf",prio=100,
     eff={"setFlags":["main.case_opened"]},tags=["main"])
line("DL_NOB_WHARF_D1_007","young_noble","wharf_12",1,"dawn","forced_node",
     "无多余的话，转身走进雾里（离场）",event="prologue_wharf",prio=100,
     eff={"npcState":{"young_noble":{"present":False}}},tags=["main"])
line("DL_NOB_WHARF_D1_008","young_noble","wharf_12",1,None,"idle_fallback",
     "已获联络方式后再次搭话，只是点头",prio=10,once=False,tags=["fallback"])
line("DL_NOB_WHARF_D1_009","young_noble","wharf_12",1,"dawn","npc_initiative",
     "变体：玩家选项命中两处以上痕迹，多留一句赞许",prio=40,group="nob_d1_variant",
     chain={"mode":"count","entries":[{"node":"CN_D1_WHARF_EVIDENCE"}],"minCount":2},tags=["variant"])
line("DL_NOB_WHARF_D1_010","young_noble","wharf_12",1,"dawn","npc_initiative",
     "变体：玩家选项莽撞，语气更冷，仅留联络方式",prio=40,group="nob_d1_variant",
     rel={"young_noble":{"trust":{"<=":0}}},tags=["variant"])

line("DL_NOB_MANSION_D4_010","young_noble","mansion_study",4,"afternoon","forced_node",
     "在书房等候，起身，直接切入正题",event="F2_mansion_photo",prio=100,tags=["main","F2"])
line("DL_NOB_MANSION_D4_011","young_noble","mansion_study",4,"afternoon","forced_node",
     "说出钻石的名字——东方之星",event="F2_mansion_photo",prio=100,
     eff={"setFlags":["main.diamond_identity"]},tags=["main","F2"])
line("DL_NOB_MANSION_D4_012","young_noble","mansion_study",4,"afternoon","forced_node",
     "解释钻石是信托基金凭证，丢失即丧失合法持有人身份",event="F2_mansion_photo",prio=100,tags=["main","F2"])
line("DL_NOB_MANSION_D4_013","young_noble","mansion_study",4,"afternoon","forced_node",
     "点明旁支正在争这个，动机在此",event="F2_mansion_photo",prio=100,
     eff={"setFlags":["main.branch_motive"]},tags=["main","F2"])
line("DL_NOB_MANSION_D4_014","young_noble","mansion_study",4,"afternoon","forced_node",
     "取出钻石档案供查阅",event="F2_mansion_photo",prio=100,
     eff={"addItems":["diamond_archive"]},tags=["main","F2"])
line("DL_NOB_MANSION_D4_015","young_noble","mansion_study",4,"afternoon","forced_node",
     "交付银质封印戒指与授信函",event="F2_mansion_photo",prio=100,
     eff={"setFlags":["main.seal_ring"],"addItems":["seal_ring","credit_letter"]},
     action="推过桌面",tags=["main","F2"])
line("DL_NOB_MANSION_D4_016","young_noble","mansion_study",4,"afternoon","choice_response",
     "选项a：听玩家汇报进展，就某一处提出补充",event="F2_mansion_photo",prio=60,group="nob_d4_reply",tags=["main"])
line("DL_NOB_MANSION_D4_017","young_noble","mansion_study",4,"afternoon","choice_response",
     "选项b：详述钻石背景与家族信托条款细节",event="F2_mansion_photo",prio=60,group="nob_d4_reply",tags=["main"])
line("DL_NOB_MANSION_D4_018","young_noble","mansion_study",4,"afternoon","forced_node",
     "从书下抽出旧照片，说是在文件里发现的，不知是谁",event="F2_mansion_photo",prio=100,
     action="递出照片",tags=["main","F2","side_origin"])
line("DL_NOB_MANSION_D4_019","young_noble","mansion_study",4,"afternoon","choice_response",
     "选项c：玩家提及照片女子，他迟疑，未多问",event="F2_mansion_photo",prio=60,group="nob_d4_reply",tags=["side_origin"])
line("DL_NOB_MANSION_D4_020","young_noble","mansion_study",4,"afternoon","choice_response",
     "选项d：玩家直接开口借照片",event="F2_mansion_photo",prio=60,group="nob_d4_reply",tags=["side_origin"])
line("DL_NOB_MANSION_D4_021","young_noble","mansion_study",4,"afternoon","forced_node",
     "答应借出——只两个字",event="F2_mansion_photo",prio=100,
     eff={"addItems":["old_silver_photo"],"setFlags":["hidden.photo_acquired","hidden.face_resemblance"]},
     tags=["main","F2","side_origin"])
line("DL_NOB_MANSION_D4_022","young_noble","mansion_study",4,"afternoon","npc_initiative",
     "变体：玩家始终没提照片，他主动递过来的版本",event="F2_mansion_photo",prio=40,
     group="nob_d4_photo",forb=("hidden.photo_acquired",),tags=["variant","side_origin"])
line("DL_NOB_MANSION_D4_023","young_noble","mansion_study",4,"afternoon","idle_fallback",
     "送客，简短",prio=10,once=False,tags=["fallback"])
line("DL_NOB_MANSION_D4_024","young_noble","mansion_study",4,"afternoon","npc_initiative",
     "变体：玩家已持有照片，补一句关于照片背面的细节",prio=40,group="nob_d4_extra",
     req=("hidden.photo_acquired",),tags=["variant","side_origin"])
line("DL_NOB_MANSION_D4_025","young_noble","mansion_study",4,"afternoon","npc_initiative",
     "变体：玩家已找到空木盒，他对红绸包裹作出回应",prio=40,group="nob_d4_extra",
     req=("main.empty_box",),tags=["variant"])

line("DL_NOB_MANSION_D6_030","young_noble","mansion_study",6,"afternoon","enter_scene",
     "看着桌上的钻石，轻声一句道谢",event="D6_action_return",prio=80,req=("main.diamond_recovered",),tags=["main"])
line("DL_NOB_MANSION_D6_031","young_noble","mansion_study",6,"afternoon","choice_response",
     "选项a：被问是否查过她，取出一页记录",event="D6_action_return",prio=60,group="nob_d6_reply",tags=["side_origin"])
line("DL_NOB_MANSION_D6_032","young_noble","mansion_study",6,"afternoon","forced_node",
     "念出雇佣记录：姓名、年份、职位、未婚无亲属",event="D6_action_return",prio=100,
     eff={"setFlags":["hidden.mother_named"]},tags=["main","side_origin"])
line("DL_NOB_MANSION_D6_033","young_noble","mansion_study",6,"afternoon","forced_node",
     "指出父亲补的那行小字——据称离开后生育一名男婴",event="D6_action_return",prio=100,
     eff={"setFlags":["hidden.birth_year_gap"]},tags=["main","side_origin"])
line("DL_NOB_MANSION_D6_034","young_noble","mansion_study",6,"afternoon","choice_response",
     "选项b：玩家沉默或自报出生年份，他的反应",event="D6_action_return",prio=60,group="nob_d6_reply",tags=["side_origin"])
line("DL_NOB_MANSION_D6_035","young_noble","mansion_study",6,"afternoon","choice_response",
     "选项c：玩家推回记录不要，他坚持让对方带走",event="D6_action_return",prio=60,group="nob_d6_reply",tags=["side_origin"])
line("DL_NOB_MANSION_D6_036","young_noble","mansion_study",6,"afternoon","choice_response",
     "选项d：玩家要求继续追查七橡树镇，他给出保留答复",event="D6_action_return",prio=60,group="nob_d6_reply",tags=["side_origin"])
line("DL_NOB_MANSION_D6_037","young_noble","mansion_study",6,"afternoon","forced_node",
     "把记录推过桌面——您可以带走",event="D6_action_return",prio=100,action="推过桌面",
     eff={"addItems":["maid_record"]},tags=["main","side_origin"])
line("DL_NOB_MANSION_D6_038","young_noble","mansion_study",6,"afternoon","npc_initiative",
     "变体：关系值≥3，告别时多一句关于身世的私人表态",prio=40,group="nob_d6_farewell",
     rel={"young_noble":{"trust":{">=":3}}},tags=["variant","side_origin"])
line("DL_NOB_MANSION_D6_039","young_noble","mansion_study",6,"afternoon","npc_initiative",
     "变体：关系值<3，维持委托人距离，只谈案子",prio=40,group="nob_d6_farewell",
     rel={"young_noble":{"trust":{"<":3}}},tags=["variant"])
line("DL_NOB_MANSION_D6_040","young_noble","mansion_study",6,"afternoon","enter_scene",
     "失败分支：钻石未追回时的会面，语气与内容整体改写",prio=80,group="nob_d6_open",
     forb=("main.diamond_recovered",),tags=["variant","fail_branch"])

line("DL_NOB_COURT_D7_050","young_noble","court",7,"morning","enter_scene",
     "旁听席上的一句话，克制",event="F3_court",prio=60,tags=["main","F3"])
line("DL_NOB_COURT_D7_051","young_noble","court",7,"morning","npc_initiative",
     "变体：羁押判决后对玩家的简短致意",event="F3_court",prio=40,group="nob_court_end",
     req=("main.lawyer_convicted",),tags=["variant","F3"])
line("DL_NOB_COURT_D7_052","young_noble","court",7,"morning","npc_initiative",
     "变体：保释判决后的一句失望但不指责",event="F3_court",prio=40,group="nob_court_end",
     req=("main.lawyer_bailed",),tags=["variant","F3","fail_branch"])
line("DL_NOB_CORRIDOR_D7_053","young_noble","court_corridor",7,"afternoon","enter_scene",
     "走廊相遇，提及信托基金保住了",prio=60,tags=["main"])
line("DL_NOB_CORRIDOR_D7_054","young_noble","court_corridor",7,"afternoon","npc_initiative",
     "暗线：若照片线索在手，主动提她的后续",prio=40,req=("hidden.photo_acquired",),tags=["side_origin","hidden"])

# ===== 8.2 警探 (38) =====
line("DL_DET_WHARF_D1_001","detective","wharf_12",1,"morning","enter_scene",
     "对记者的初始态度：拍完照片就走",event="D1_action1_search",prio=80,emotion="cold",tags=["main"])
line("DL_DET_WHARF_D1_002","detective","wharf_12",1,"morning","choice_response",
     "玩家指出底板缝隙木刺断裂，他愣住",event="D1_action1_search",prio=100,emotion="neutral",
     eff={"relationship":{"detective":1}},tags=["main"])
line("DL_DET_WHARF_D1_003","detective","wharf_12",1,"morning","forced_node",
     "随即喊人撬开夹层",event="D1_action1_search",prio=100,action="转身喊人",tags=["main"])
line("DL_DET_WHARF_D1_004","detective","wharf_12",1,"morning","forced_node",
     "撬开后承认记者眼力，态度转折",event="D1_action1_search",prio=100,
     eff={"relationship":{"detective":1}},tags=["main"])
line("DL_DET_WHARF_D1_005","detective","wharf_12",1,"morning","choice_response",
     "选项a：对夹层锦缎与圆形压痕的判断，确认是粉钻碎屑",event="D1_action1_search",prio=60,
     group="det_d1_evidence",eff={"setFlags":["main.found_pink_shards"],"addItems":["pink_shard","red_velvet"],
     "relationship":{"detective":1},"consumeAction":True},tags=["main"])
line("DL_DET_WHARF_D1_006","detective","wharf_12",1,"morning","choice_response",
     "选项b：对地面拖痕方向的判断，指向冷藏库",event="D1_action1_search",prio=60,
     group="det_d1_evidence",eff={"setFlags":["main.found_drag_marks"],"addItems":["red_fiber"],
     "consumeAction":True},tags=["main"])
line("DL_DET_WHARF_D1_007","detective","wharf_12",1,"morning","choice_response",
     "选项c：听完工人口供后的记录与追问",event="D1_action1_search",prio=60,
     group="det_d1_evidence",eff={"setFlags":["main.worker_testimony"],"consumeAction":True},tags=["main"])
line("DL_DET_WHARF_D1_008","detective","wharf_12",1,"morning","choice_response",
     "选项d：对登记簿签收栏空白的质疑",event="D1_action1_search",prio=60,
     group="det_d1_evidence",eff={"setFlags":["main.registry_gap"],"addItems":["registry_page"],
     "consumeAction":True},tags=["main"])
line("DL_DET_WHARF_D1_009","detective","wharf_12",1,"morning","idle_fallback",
     "收束（trust低）：警告别乱写",prio=30,group="det_d1_close",once=False,
     rel={"detective":{"trust":{"<":2}}},tags=["variant"])
line("DL_DET_WHARF_D1_010","detective","wharf_12",1,"morning","idle_fallback",
     "收束（trust≥2）：邀去警局找他",prio=30,group="det_d1_close",once=False,
     rel={"detective":{"trust":{">=":2}}},tags=["variant"])
line("DL_DET_WHARF_D1_011","detective","wharf_12",1,None,"idle_fallback",
     "重复勘查时的无事可说",prio=10,once=False,tags=["fallback"])

line("DL_DET_POLICE_D2_020","detective","police_station",2,"morning","enter_scene",
     "主动给出签到未签离岗的四晚记录，含18号",event="D2_action_police",prio=80,
     eff={"setFlags":["main.absence_pattern"]},tags=["main"])
line("DL_DET_POLICE_D2_021","detective","police_station",2,"morning","forced_node",
     "转述工人所见：黑色马车停后巷半小时，往内城",event="D2_action_police",prio=100,
     eff={"setFlags":["main.black_carriage"]},tags=["main"])
line("DL_DET_POLICE_D2_022","detective","police_station",2,"morning","choice_response",
     "选项a：被追问经理动机，给出欠债的初步判断",prio=60,group="det_d2_reply",tags=["main"])
line("DL_DET_POLICE_D2_023","detective","police_station",2,"morning","choice_response",
     "选项b：玩家提及海关不计价记录，他要求看原件",prio=60,group="det_d2_reply",
     req=("main.customs_unvalued",),tags=["main"])
line("DL_DET_POLICE_D2_024","detective","police_station",2,"morning","choice_response",
     "选项c：玩家出示酒馆匿名纸条，态度明显转变",prio=60,group="det_d2_reply",
     req=("main.anon_note",),eff={"relationship":{"detective":1}},tags=["main"])
line("DL_DET_POLICE_D2_025","detective","police_station",2,"morning","choice_response",
     "选项d：玩家保留情报不说，他不悦",prio=60,group="det_d2_reply",
     eff={"relationship":{"detective":-1}},emotion="hostile",tags=["variant"])
line("DL_DET_POLICE_D2_026","detective","police_station",2,None,"idle_fallback",
     "兜底：忙着写报告",prio=10,once=False,tags=["fallback"])

line("DL_DET_POLICE_D3_030","detective","police_station",3,"morning","npc_initiative",
     "变体：已获短函，主动提出去申请搜查令",prio=40,group="det_d3_state",
     req=("main.typed_memo",),tags=["variant"])
line("DL_DET_POLICE_D3_031","detective","police_station",3,"morning","npc_initiative",
     "变体：未获短函，抱怨手上没证据无法动经理",prio=40,group="det_d3_state",
     forb=("main.typed_memo",),tags=["variant","fail_branch"])
line("DL_DET_POLICE_D4_032","detective","police_station",4,"morning","npc_initiative",
     "变体：F2已过，得知记者拿到授信函后的反应",prio=40,group="det_d4_state",
     req=("main.seal_ring",),tags=["variant"])
line("DL_DET_WHARF_D4_033","detective","wharf_12",4,"morning","enter_scene",
     "D4码头再遇，简短通报进度",prio=60,tags=["main"])

line("DL_DET_POLICE_D5_040","detective","police_station",5,"morning","item_inspect",
     "看完妻子的信，判断经理是被利用的",event="D5_action_police",prio=80,item="wife_letter",
     req=("main.wife_letter",),eff={"setFlags":["main.manager_used"]},tags=["main"])
line("DL_DET_POLICE_D5_041","detective","police_station",5,"morning","forced_node",
     "确认派人去肯特郡取文件",event="D5_action_police",prio=100,tags=["main"])
line("DL_DET_POLICE_D5_042","detective","police_station",5,"morning","forced_node",
     "提醒：你那个主编昨天又来打听案情",event="D5_action_police",prio=100,
     eff={"setFlags":["hidden.editor_aware"]},tags=["main","hidden"])
line("DL_DET_POLICE_D5_043","detective","police_station",5,"morning","choice_response",
     "选项a：被追问主编，给出谨慎评价",prio=60,group="det_d5_reply",tags=["hidden"])
line("DL_DET_POLICE_D5_044","detective","police_station",5,"morning","choice_response",
     "选项b：玩家请求保护经理妻子，他安排",prio=60,group="det_d5_reply",
     eff={"setFlags":["hidden.wife_protected"]},tags=["side"])
line("DL_DET_POLICE_D5_045","detective","police_station",5,"morning","choice_response",
     "选项c：玩家隐瞒发缕与照片，他察觉有所保留",prio=60,group="det_d5_reply",
     req=("hidden.hair_sample",),eff={"relationship":{"detective":-1}},tags=["variant","hidden"])
line("DL_DET_POLICE_D5_046","detective","police_station",5,"morning","choice_response",
     "选项d：玩家提出隧道与空木盒，他震惊并要求带路",prio=60,group="det_d5_reply",
     req=("main.empty_box",),tags=["main"])
line("DL_DET_POLICE_D5_047","detective","police_station",5,None,"idle_fallback",
     "兜底",prio=10,once=False,tags=["fallback"])

line("DL_DET_COURT_D7_050","detective","court",7,"morning","forced_node",
     "庭上宣读指控",event="F3_court",prio=100,tags=["main","F3"])
line("DL_DET_COURT_D7_051","detective","court",7,"morning","npc_initiative",
     "变体：庭审证据链完整，追加指控，措辞更强硬",event="F3_court",prio=40,group="det_court_charge",
     req=("main.wife_letter","main.manager_confessed"),tags=["variant","F3"])
line("DL_DET_COURT_D7_052","detective","court",7,"morning","npc_initiative",
     "变体：证据薄弱，只能提程序性问题",event="F3_court",prio=40,group="det_court_charge",
     forb=("main.wife_letter","main.manager_confessed"),tags=["variant","F3","fail_branch"])
line("DL_DET_COURT_D7_053","detective","court",7,"afternoon","npc_initiative",
     "变体：trust≥3，庭后对记者的认可评价",prio=40,group="det_court_after",
     rel={"detective":{"trust":{">=":3}}},tags=["variant"])
line("DL_DET_COURT_D7_054","detective","court",7,"afternoon","npc_initiative",
     "变体：trust<3，庭后的疏离表态",prio=40,group="det_court_after",
     rel={"detective":{"trust":{"<":3}}},tags=["variant"])
line("DL_DET_POLICE_D7_060","detective","police_station",7,"afternoon","forced_node",
     "转交从七橡树镇带回的铁盒",event="D7_action_police",prio=100,
     eff={"addItems":["iron_box_letters"],"setFlags":["main.twelve_letters"]},tags=["main"])
line("DL_DET_POLICE_D7_061","detective","police_station",7,"afternoon","forced_node",
     "念出二月与五月十二日两封关键信函要点",event="D7_action_police",prio=100,tags=["main"])
line("DL_DET_POLICE_D7_062","detective","police_station",7,"afternoon","forced_node",
     "结案陈词，含对经理的量刑态度",event="D7_action_police",prio=100,
     eff={"setFlags":["main.case_closed"]},tags=["main","ending"])
line("DL_DET_POLICE_D7_063","detective","police_station",7,"afternoon","npc_initiative",
     "变体：暗线未揭，留一句关于主编的疑问",prio=40,group="det_ending_extra",
     forb=("hidden.editor_bribe",),tags=["hidden","variant"])
line("DL_DET_WHARF_D2_070","detective","wharf_12",2,"morning","enter_scene",
     "D2码头偶遇，一句话进度通报",prio=30,once=False,tags=["variant"])
line("DL_DET_WHARF_D3_071","detective","wharf_12",3,"morning","enter_scene",
     "D3码头偶遇，语气随信任度变化",prio=30,once=False,tags=["variant"])
line("DL_DET_WHARF_D5_072","detective","wharf_12",5,"morning","enter_scene",
     "D5码头偶遇，提及冷藏库已被封锁",prio=30,once=False,tags=["variant"])
line("DL_DET_COLD_D5_073","detective","cold_storage",5,"night","npc_initiative",
     "变体：玩家在冷藏库被警探撞见，他的质问",prio=40,group="det_cold_bust",tags=["variant"])
line("DL_DET_POLICE_D6_074","detective","police_station",6,"morning","enter_scene",
     "D6警局：通报经理已被拘押",prio=60,req=("main.manager_confessed",),tags=["main"])
line("DL_DET_POLICE_D6_075","detective","police_station",6,"morning","choice_response",
     "选项回应：玩家请求查阅经理供词全文",prio=60,group="det_d6_reply",tags=["main"])
line("DL_DET_POLICE_D6_076","detective","police_station",6,"morning","choice_response",
     "选项回应：玩家追问戴面纱女性的身份推测",prio=60,group="det_d6_reply",
     req=("main.veil_woman",),tags=["main"])
line("DL_DET_POLICE_D6_077","detective","police_station",6,"morning","idle_fallback",
     "兜底：等肯特郡那边回话",prio=10,once=False,tags=["fallback"])
line("DL_DET_COURT_D7_078","detective","court",7,"morning","idle_fallback",
     "庭审间歇兜底：整理卷宗",prio=10,once=False,tags=["fallback"])

# ===== 8.3 老板娘 (26) =====
line("DL_LND_TAVERN_D1_001","landlady","tavern",1,"dusk","forced_node",
     "倒一杯黑啤酒推过来，不说话先打量",event="F1_tavern_reveal",prio=100,
     action="推过酒杯",tags=["main","F1"])
line("DL_LND_TAVERN_D1_002","landlady","tavern",1,"dusk","forced_node",
     "核心情报：那箱子里有夹层",event="F1_tavern_reveal",prio=100,
     eff={"setFlags":["main.know_hidden_layer"]},tags=["main","F1"])
line("DL_LND_TAVERN_D1_003","landlady","tavern",1,"dusk","forced_node",
     "表弟在多佛亲眼所见：箱底锦缎裹着鸽蛋大的圆粉石",event="F1_tavern_reveal",prio=100,tags=["main","F1"])
line("DL_LND_TAVERN_D1_004","landlady","tavern",1,"dusk","forced_node",
     "箱子是贵族家老管家亲自经手的",event="F1_tavern_reveal",prio=100,
     eff={"setFlags":["main.know_dover_route"]},tags=["main","F1"])
line("DL_LND_TAVERN_D1_005","landlady","tavern",1,"dusk","choice_response",
     "选项a：追问夹层细节，补出锦缎颜色与包裹方式",prio=60,group="lnd_d1_reply",tags=["main"])
line("DL_LND_TAVERN_D1_006","landlady","tavern",1,"dusk","choice_response",
     "选项b：询问老管家身份，给出姓氏与在府年限",prio=60,group="lnd_d1_reply",tags=["main"])
line("DL_LND_TAVERN_D1_007","landlady","tavern",1,"dusk","choice_response",
     "选项c：玩家表明不会乱写，她松口多给一条",prio=60,group="lnd_d1_reply",
     eff={"relationship":{"landlady":1}},tags=["main"])
line("DL_LND_TAVERN_D1_008","landlady","tavern",1,"dusk","choice_response",
     "选项d：追问消息来源，她立刻戒备、收回话头",prio=60,group="lnd_d1_reply",
     eff={"relationship":{"landlady":-1}},emotion="evasive",tags=["variant"])
line("DL_LND_TAVERN_D1_009","landlady","tavern",1,"dusk","npc_initiative",
     "变体：玩家D1已在码头发现碎屑时的额外一句",prio=40,group="lnd_d1_extra",
     req=("main.found_pink_shards",),tags=["variant"])
line("DL_LND_TAVERN_D1_010","landlady","tavern",1,None,"idle_fallback",
     "兜底：酒钱照付，别在这儿写稿",prio=10,once=False,tags=["fallback"])

line("DL_LND_TAVERN_D2_020","landlady","tavern",2,"dusk","enter_scene",
     "第2次访问：不问先递来一张匿名纸条",event="D2_action_tavern",prio=80,visit=2,
     action="递出纸条",eff={"addItems":["anon_note"]},tags=["main"])
line("DL_LND_TAVERN_D2_021","landlady","tavern",2,"dusk","forced_node",
     "纸条内容指向：粉石头交接点在皇后公园站",event="D2_action_tavern",prio=100,
     eff={"setFlags":["main.anon_note"]},tags=["main"])
line("DL_LND_TAVERN_D2_022","landlady","tavern",2,"dusk","choice_response",
     "选项a：追问纸条来路，只说有人留下就走了",prio=60,group="lnd_d2_reply",tags=["main"])
line("DL_LND_TAVERN_D2_023","landlady","tavern",2,"dusk","choice_response",
     "选项b：询问皇后公园站，补出储物柜编号线索",prio=60,group="lnd_d2_reply",tags=["main"])
line("DL_LND_TAVERN_D2_024","landlady","tavern",2,"dusk","choice_response",
     "选项c：玩家用警探情报交换，她给出额外一条",prio=60,group="lnd_d2_reply",
     req=("main.black_carriage",),tags=["main"])
line("DL_LND_TAVERN_D2_025","landlady","tavern",2,"dusk","choice_response",
     "选项d：玩家质疑她为何帮忙，她给出自己的理由",prio=60,group="lnd_d2_reply",tags=["side"])
line("DL_LND_TAVERN_D2_026","landlady","tavern",2,None,"idle_fallback",
     "兜底",prio=10,once=False,tags=["fallback"])

line("DL_LND_TAVERN_D4_030","landlady","tavern",4,"dusk","enter_scene",
     "第3次访问：今天凌晨四点，经理妻子拎皮箱离家",event="D4_action_tavern",prio=80,visit=3,tags=["main"])
line("DL_LND_TAVERN_D4_031","landlady","tavern",4,"dusk","forced_node",
     "叫马车往南过桥，住进天鹅与皇冠旅馆",event="D4_action_tavern",prio=100,
     eff={"setFlags":["main.wife_fled"]},tags=["main"])
line("DL_LND_TAVERN_D4_032","landlady","tavern",4,"dusk","choice_response",
     "选项a：追问皮箱大小与内容猜测",prio=60,group="lnd_d4_reply",tags=["main"])
line("DL_LND_TAVERN_D4_033","landlady","tavern",4,"dusk","choice_response",
     "选项b：询问是谁替她叫的马车，给出车夫特征",prio=60,group="lnd_d4_reply",tags=["main"])
line("DL_LND_TAVERN_D4_034","landlady","tavern",4,"dusk","choice_response",
     "选项c：问及经理欠债，说出妻子医药费的数目传闻",prio=60,group="lnd_d4_reply",tags=["main"])
line("DL_LND_TAVERN_D4_035","landlady","tavern",4,"dusk","choice_response",
     "选项d：玩家问及律师是否常来酒馆，她的回避",prio=60,group="lnd_d4_reply",
     eff={"setFlags":["side.lawyer_seen_tavern"]},emotion="evasive",tags=["side"])
line("DL_LND_TAVERN_D4_036","landlady","tavern",4,None,"idle_fallback",
     "兜底",prio=10,once=False,tags=["fallback"])

line("DL_LND_TAVERN_D6_040","landlady","tavern",6,"night","enter_scene",
     "结案传闻传开后，酒馆里的一句闲谈",prio=30,once=False,tags=["variant"])
line("DL_LND_TAVERN_D6_041","landlady","tavern",6,"night","npc_initiative",
     "暗线：提及主编曾来酒馆打听过记者的行踪",prio=40,group="lnd_hidden",
     eff={"setFlags":["hidden.editor_aware"]},tags=["hidden"])
line("DL_LND_TAVERN_D7_042","landlady","tavern",7,"night","enter_scene",
     "最终一句，含对经理妻子的同情",prio=60,tags=["ending"])
line("DL_LND_TAVERN_D7_043","landlady","tavern",7,"night","npc_initiative",
     "失败分支变体：钻石未追回时的版本",prio=40,group="lnd_ending",
     forb=("main.diamond_recovered",),tags=["variant","fail_branch"])

# ===== 8.4 律师 (20) =====
line("DL_LAW_LAWOFF_D4_001","lawyer","lawyer_office",4,"morning","enter_scene",
     "D4办公室无人，仅留一张外出便条",event="D4_action_lawoff",prio=80,typ="narration",
     eff={"npcState":{"lawyer":{"present":False}}},tags=["main"])
line("DL_LAW_LAWOFF_D5_002","lawyer","lawyer_office",5,"morning","npc_initiative",
     "变体：玩家已查出Wapping出差，此处补一句他刚回来的痕迹",prio=40,
     req=("main.lawyer_wapping",),tags=["variant"])
line("DL_LAW_COURT_D7_010","lawyer","court",7,"morning","forced_node",
     "出庭：黑西装熨烫平整，金边眼镜反冷光，一句程序性发言",event="F3_court",prio=100,
     emotion="cold",tags=["main","F3"])
line("DL_LAW_COURT_D7_011","lawyer","court",7,"morning","forced_node",
     "第一反应：我的委托人——旁支——有合法主张权",event="F3_court",prio=100,tags=["main","F3"])
line("DL_LAW_COURT_D7_012","lawyer","court",7,"morning","choice_response",
     "选项a：玩家出示妻子的信，他质疑信件来源与合法性",event="F3_court",prio=60,
     group="court_lawyer_rebuttal",req=("main.wife_letter",),tags=["main","F3"])
line("DL_LAW_COURT_D7_013","lawyer","court",7,"morning","choice_response",
     "选项b：玩家指出前妻身份，他说不明白您在说什么",event="F3_court",prio=60,
     group="court_lawyer_rebuttal",req=("main.veil_woman",),action="推了推眼镜",
     eff={"setFlags":["main.ex_wife_identity"]},tags=["main","F3"])
line("DL_LAW_COURT_D7_014","lawyer","court",7,"morning","choice_response",
     "选项c：玩家提及已付清纸条，他的手指在桌上停住",event="F3_court",prio=60,
     group="court_lawyer_rebuttal",req=("hidden.editor_bribe",),action="手指在桌上停住",tags=["main","F3"])
line("DL_LAW_COURT_D7_015","lawyer","court",7,"morning","choice_response",
     "选项d：玩家出示经理的完整供词，他转而攻击供词的取证程序",event="F3_court",prio=60,
     group="court_lawyer_rebuttal",req=("main.manager_confessed",),emotion="cold",tags=["main","F3"])
line("DL_LAW_COURT_D7_016","lawyer","court",7,"morning","choice_response",
     "选项d后续：庭审证据链完整（妻子的信＋经理供词）时，防线崩塌的一句",event="F3_court",prio=60,
     group="court_lawyer_rebuttal",req=("main.wife_letter","main.manager_confessed"),
     emotion="collapsing",tags=["main","F3"])
line("DL_LAW_COURT_D7_017","lawyer","court",7,"morning","npc_initiative",
     "变体：证据薄弱时的强势反将，要求撤案",event="F3_court",prio=55,group="lawyer_final",
     forb=("main.wife_letter","main.manager_confessed"),emotion="threatening",tags=["variant","F3","fail_branch"])
line("DL_LAW_COURT_D7_041","lawyer","court",7,"morning","npc_initiative",
     "变体：庭审举证结束后的收束陈词（同组兜底，恒可播）",event="F3_court",prio=50,
     group="lawyer_final",emotion="cold",tags=["variant","F3"])
line("DL_LAW_COURT_D7_018","lawyer","court",7,"morning","npc_initiative",
     "暗线：玩家持有主编受贿证据时，一句带威胁的试探",prio=40,group="lawyer_hidden",
     req=("hidden.editor_bribe",),tone="whisper",tags=["hidden","F3"])
line("DL_LAW_COURT_D7_019","lawyer","court",7,"morning","npc_initiative",
     "暗线：身世线暴露，暗示可用记者的出身做文章",prio=40,group="lawyer_hidden",
     req=("hidden.mother_named",),tone="whisper",tags=["hidden","side_origin","F3"])
line("DL_LAW_COURT_D7_020","lawyer","court",7,"morning","npc_initiative",
     "失败结局：辩方胜诉后的陈词，冷静、无胜利姿态",prio=40,group="lawyer_end",
     req=("main.lawyer_bailed",),tags=["variant","fail_branch"])
line("DL_LAW_CORRIDOR_D7_030","lawyer","court_corridor",7,"afternoon","forced_node",
     "走廊被低声质问，停一步、不回头",prio=100,action="停了一步，没有回头",tags=["main"])
line("DL_LAW_CORRIDOR_D7_031","lawyer","court_corridor",7,"afternoon","npc_initiative",
     "变体：玩家没开口时，他反倒先说一句",prio=40,group="corridor_variant",tags=["variant"])
line("DL_LAW_CORRIDOR_D7_032","lawyer","court_corridor",7,"afternoon","npc_initiative",
     "变体：定罪后被带离时的一句，不带情绪",prio=40,group="corridor_variant",
     req=("main.lawyer_convicted",),tags=["variant"])
line("DL_LAW_COURT_D7_033","lawyer","court",7,"morning","forced_node",
     "保释获准、限制出境时向法官的一句陈述",prio=100,target="judge",tags=["main","F3"])
line("DL_LAW_LAWOFF_D7_034","lawyer","lawyer_office",7,"afternoon","npc_initiative",
     "支线：庭后去办公室搜查，发现遗留物与一封未寄出的信",prio=30,tags=["side"])
line("DL_LAW_COURT_D7_035","lawyer","court",7,"morning","idle_fallback",
     "庭审间歇的程序性套话",prio=10,once=False,tags=["fallback"])
line("DL_LAW_COURT_D7_036","lawyer","court",7,"morning","idle_fallback",
     "对法官提问的标准应答（可复用于多个回合）",prio=20,once=False,target="judge",tags=["fallback"])

# ===== 8.5 主编 (16) =====
line("DL_EDT_NEWS_D1_001","editor","newsroom",1,"dawn","forced_node",
     "清晨派活：去12号码头采访贵族货物失窃",event="D1_action2_news",prio=100,tags=["main"])
line("DL_EDT_NEWS_D1_002","editor","newsroom",1,"afternoon","enter_scene",
     "见记者回来，只问两句：谁托运的、保额多少",event="D1_action2_news",prio=80,tags=["main","hidden"])
line("DL_EDT_NEWS_D1_003","editor","newsroom",1,"afternoon","choice_response",
     "选项a：玩家如实报出保额，他记下，不表态",prio=60,group="edt_d1_reply",tags=["hidden"])
line("DL_EDT_NEWS_D1_004","editor","newsroom",1,"afternoon","choice_response",
     "选项b：玩家反问为何更关心保额，他岔开话题",prio=60,group="edt_d1_reply",tags=["hidden"])
line("DL_EDT_NEWS_D1_005","editor","newsroom",1,"afternoon","choice_response",
     "选项c：玩家提及桌上那封来自律师的信，他迅速收起",prio=60,group="edt_d1_reply",
     action="迅速收起信件",tags=["hidden"])
line("DL_EDT_NEWS_D1_006","editor","newsroom",1,"afternoon","choice_response",
     "选项d：玩家隐瞒发现，他表示满意，语气如常",prio=60,group="edt_d1_reply",tags=["hidden"])
line("DL_EDT_NEWS_D1_007","editor","newsroom",1,None,"idle_fallback",
     "兜底：低头改稿，不看人",prio=10,once=False,tags=["fallback"])
line("DL_EDT_NEWS_D3_010","editor","newsroom",3,"afternoon","enter_scene",
     "D3报社：桌上摊着一封写了一半的信，收信人是律师",event="D3_action_news",prio=80,visit=2,
     eff={"setFlags":["side.letter_half"]},tags=["hidden"])
line("DL_EDT_NEWS_D3_011","editor","newsroom",3,"afternoon","choice_response",
     "被问及那封信，一句轻描淡写的解释",prio=60,group="edt_d3_reply",tags=["hidden"])
line("DL_EDT_NEWS_D3_012","editor","newsroom",3,None,"idle_fallback",
     "兜底：催稿",prio=10,once=False,tags=["fallback"])
line("DL_EDT_NEWS_D5_020","editor","newsroom",5,"afternoon","enter_scene",
     "D5主动打听案情进展（与警探转述互为印证）",prio=80,visit=3,
     eff={"setFlags":["hidden.editor_aware"]},tags=["hidden"])
line("DL_EDT_NEWS_D5_021","editor","newsroom",5,"afternoon","choice_response",
     "玩家敷衍，他不再追问，但记下了",prio=60,group="edt_d5_reply",tags=["hidden"])
line("DL_EDT_NEWS_D7_030","editor","newsroom",7,"night","forced_node",
     "深夜经过，看完稿子只说三个字：明天头版",event="D7_action_final",prio=100,tags=["main","ending"])
line("DL_EDT_NEWS_D7_031","editor","newsroom",7,"night","choice_response",
     "选项a：玩家当面质问资金往来，他的沉默与一句回避",prio=60,group="edt_d7_reply",
     req=("hidden.editor_bribe",),tags=["hidden","ending"])
line("DL_EDT_NEWS_D7_032","editor","newsroom",7,"night","choice_response",
     "选项b：玩家沉默交稿，他语气如常地道一句辛苦",prio=60,group="edt_d7_reply",tags=["ending"])
line("DL_EDT_NEWS_D7_033","editor","newsroom",7,"night","choice_response",
     "选项c：玩家提及七橡树镇，他有极轻微的反应",prio=60,group="edt_d7_reply",
     req=("main.know_kent_address",),tags=["hidden","side_origin"])

# ===== 8.6 码头经理 (15) =====
line("DL_MGR_WHARF_D1_001","dock_manager","wharf_12",1,"morning","enter_scene",
     "在码头被记者搭话，回避、借口公务走开",event="D1_action1_search",prio=80,
     emotion="evasive",tags=["main"])
line("DL_MGR_WHARF_D2_002","dock_manager","wharf_12",2,"morning","enter_scene",
     "匆忙经过，一句敷衍",prio=30,visit=2,emotion="evasive",tags=["variant"])
line("DL_MGR_MGOFF_D3_003","dock_manager","manager_office",3,"morning","enter_scene",
     "办公室无人（旁白说明），供搜查发生",event="D3_action_mgoff",prio=80,typ="narration",
     eff={"npcState":{"dock_manager":{"present":False}}},tags=["main"])
line("DL_MGR_COLD_D5_004","dock_manager","cold_storage",5,"night","npc_initiative",
     "分支：玩家在冷藏库被他撞见，一句紧张质问",prio=40,group="mgr_cold_bust",
     emotion="anxious",tags=["variant"])
line("DL_MGR_POLICE_D6_010","dock_manager","police_station",6,"morning","forced_node",
     "被捕后初次供词：雇他的是戴面纱的女性",prio=100,
     eff={"setFlags":["main.veil_woman","main.manager_confessed"]},tags=["main"])
line("DL_MGR_POLICE_D6_011","dock_manager","police_station",6,"morning","forced_node",
     "交代八十镑报酬与妻子医药债",prio=100,emotion="collapsing",tags=["main"])
line("DL_MGR_POLICE_D6_012","dock_manager","police_station",6,"morning","choice_response",
     "选项a：被追问面纱女子身份，说不出更多",prio=60,group="mgr_d6_reply",tags=["main"])
line("DL_MGR_POLICE_D6_013","dock_manager","police_station",6,"morning","choice_response",
     "选项b：玩家出示其妻的信，他情绪崩溃",prio=60,group="mgr_d6_reply",
     req=("main.wife_letter",),emotion="collapsing",tags=["main"])
line("DL_MGR_POLICE_D6_014","dock_manager","police_station",6,"morning","choice_response",
     "选项c：玩家提及铅块替换，他补出作案细节",prio=60,group="mgr_d6_reply",
     req=("main.typed_memo",),tags=["main"])
line("DL_MGR_POLICE_D6_015","dock_manager","police_station",6,"morning","choice_response",
     "选项d：玩家承诺不写他妻子，他感激并多交代一条",prio=60,group="mgr_d6_reply",
     eff={"setFlags":["hidden.wife_protected"],"relationship":{"dock_manager":2}},tags=["side"])
line("DL_MGR_POLICE_D6_016","dock_manager","police_station",6,"morning","choice_response",
     "被问及台历上的圈与N.O.，给出解释",prio=60,group="mgr_d6_reply",
     req=("main.calendar_n_o",),tags=["main"])
line("DL_MGR_COURT_D7_020","dock_manager","court",7,"morning","forced_node",
     "法庭认罪陈述",event="F3_court",prio=100,tags=["main","F3"])
line("DL_MGR_COURT_D7_021","dock_manager","court",7,"morning","npc_initiative",
     "变体：玩家曾隐瞒其妻下落，他的态度与措辞变化",prio=40,group="mgr_court_variant",
     forb=("hidden.wife_protected",),tags=["variant","F3"])
line("DL_MGR_COURT_D7_022","dock_manager","court",7,"afternoon","npc_initiative",
     "变体：关系值高，庭后对记者的一句托付",prio=40,group="mgr_court_after",
     rel={"dock_manager":{"trust":{">=":2}}},tags=["variant"])
line("DL_MGR_POLICE_D7_023","dock_manager","police_station",7,None,"idle_fallback",
     "兜底：沉默，看着桌面",prio=10,once=False,tags=["fallback"])

# ===== 8.7 经理妻子 (9) =====
line("DL_WIF_HOTEL_D5_001","manager_wife","hotel_swan",5,"morning","enter_scene",
     "旅馆前台告知：已退房",event="D5_action_hotel",prio=80,typ="narration",
     eff={"npcState":{"manager_wife":{"present":False}}},tags=["main"])
line("DOC_WIF_HOTEL_D5_002","manager_wife","hotel_swan",5,"morning","item_inspect",
     "留信全文：丈夫为还债做那些事，不知箱内为何物",prio=100,typ="document",
     render="handwritten_letter",eff={"addItems":["wife_letter"],"setFlags":["main.wife_letter"]},tags=["main"])
line("DOC_WIF_HOTEL_D5_003","manager_wife","hotel_swan",5,"morning","item_inspect",
     "信中关键句：律师说替他做一件事就付八十镑",prio=100,typ="document",item="wife_letter",
     render="handwritten_letter",tags=["main"])
line("DOC_WIF_HOTEL_D5_004","manager_wife","hotel_swan",5,"morning","item_inspect",
     "信中地址：往来信函藏在肯特郡七橡树镇磨坊巷3号阁楼",prio=100,typ="document",item="wife_letter",
     render="handwritten_letter",eff={"setFlags":["main.know_kent_address"]},tags=["main"])
line("DL_WIF_HOTEL_D4_010","manager_wife","hotel_swan",4,"night","npc_initiative",
     "支线：玩家D4抢先赶到旅馆，隔门的一句（不轻易开门）",prio=40,group="wife_branch",
     tone="whisper",tags=["side"])
line("DL_WIF_HOTEL_D4_011","manager_wife","hotel_swan",4,"night","choice_response",
     "选项a：玩家追问信函藏匿位置，她隔着门说出",prio=60,group="wife_d4_reply",
     eff={"setFlags":["main.know_kent_address"]},tone="whisper",tags=["side"])
line("DL_WIF_HOTEL_D4_012","manager_wife","hotel_swan",4,"night","choice_response",
     "选项b：玩家询问是否见过戴面纱的女子，她的迟疑",prio=60,group="wife_d4_reply",tone="whisper",tags=["side"])
line("DL_WIF_HOTEL_D4_013","manager_wife","hotel_swan",4,"night","choice_response",
     "选项c：玩家先承诺保护再请她出庭，她隔着门答应了，但仍连夜离开",prio=60,group="wife_d4_reply",
     eff={"npcState":{"manager_wife":{"fled":True}}},tags=["side"])
line("DL_WIF_HOTEL_D4_014","manager_wife","hotel_swan",4,"night","choice_response",
     "选项d：玩家不做承诺直接要求出庭，她拒绝并连夜离开",prio=60,group="wife_d4_reply",
     eff={"npcState":{"manager_wife":{"fled":True}}},emotion="anxious",tags=["side"])
line("DL_WIF_COURT_D7_020","manager_wife","court",7,"morning","npc_initiative",
     "分支结局：若支线中她答应出庭，庭上的一句证词",prio=40,group="wife_court",
     req=("hidden.wife_testified",),tags=["side","variant","F3"])

# ===== 8.8 律师秘书 (6) =====
line("DL_CLK_LAWOFF_D4_001","lawyer_clerk","lawyer_office",4,"morning","forced_node",
     "低声告知：他上周去Wapping出差了三天",event="D4_action_lawoff",prio=100,tone="whisper",
     eff={"setFlags":["main.lawyer_wapping"]},tags=["main"])
line("DL_CLK_LAWOFF_D4_002","lawyer_clerk","lawyer_office",4,"morning","choice_response",
     "选项a：追问确切出差日期，给出可核对的日期",prio=60,group="clk_reply",tags=["main"])
line("DL_CLK_LAWOFF_D4_003","lawyer_clerk","lawyer_office",4,"morning","choice_response",
     "选项b：询问近期访客，提到一位戴面纱的女士",prio=60,group="clk_reply",
     eff={"setFlags":["main.veil_woman"]},tags=["main"])
line("DL_CLK_LAWOFF_D4_004","lawyer_clerk","lawyer_office",4,"morning","choice_response",
     "选项c：玩家出示记者证施压，她更紧张、说半句就收住",prio=60,group="clk_reply",
     req=("main.seal_ring",),emotion="anxious",tags=["variant"])
line("DL_CLK_LAWOFF_D4_005","lawyer_clerk","lawyer_office",4,"morning","choice_response",
     "选项d：玩家留下名片请她转交，她收下",prio=60,group="clk_reply",tags=["side"])
line("DL_CLK_LAWOFF_D6_006","lawyer_clerk","lawyer_office",6,None,"idle_fallback",
     "兜底：我不能多说",prio=10,once=False,tags=["fallback"])

# ===== 8.9 码头工人 (8) =====
line("DL_WKR_WHARF_D1_001","dock_worker","wharf_12",1,"morning","choice_response",
     "核心口供：昨晚十点半，经理推盖油布的木箱往冷藏库",event="D1_action1_search",prio=100,
     tags=["main"])
line("DL_WKR_WHARF_D1_002","dock_worker","wharf_12",1,"morning","forced_node",
     "补：空着手回来，那只木箱没回来",event="D1_action1_search",prio=100,tags=["main"])
line("DL_WKR_WHARF_D1_003","dock_worker","wharf_12",1,"morning","choice_response",
     "追问a：描述油布木箱的尺寸与重量感",prio=60,group="wkr_d1_detail",tags=["main"])
line("DL_WKR_WHARF_D1_004","dock_worker","wharf_12",1,"morning","choice_response",
     "追问b：确认经理是否独自一人",prio=60,group="wkr_d1_detail",tags=["main"])
line("DL_WKR_WHARF_D1_005","dock_worker","wharf_12",1,"morning","choice_response",
     "追问c：提及当晚还有谁在场",prio=60,group="wkr_d1_detail",tags=["main"])
line("DL_WKR_ALLEY_D2_010","dock_worker","wharf_alley",2,"morning","enter_scene",
     "后巷另一位工人谈那辆黑色马车",event="D2_action_wharf",prio=80,
     eff={"setFlags":["main.black_carriage"]},tags=["main"])
line("DL_WKR_WHARF_D3_020","dock_worker","wharf_12",3,"morning","enter_scene",
     "二次勘查时搬运工的闲谈，含一句无意线索",event="D3_action_recheck",prio=30,visit=3,once=False,tags=["variant"])
line("DL_WKR_WHARF_D1_030","dock_worker","wharf_12",1,None,"idle_fallback",
     "兜底：我们只管搬箱子",prio=10,once=False,tags=["fallback"])

# ===== 8.10 功能性角色 (12) =====
line("DL_CUS_CUSTOMS_D2_001","customs_officer","customs_house",2,"morning","enter_scene",
     "递出报关单，指出随附私人信物一件、不计价",event="D2_action_customs",prio=80,
     eff={"addItems":["customs_declaration"],"setFlags":["main.customs_unvalued"]},tags=["main"])
line("DL_CUS_CUSTOMS_D2_002","customs_officer","customs_house",2,"morning","choice_response",
     "选项a：解释不计价在实务上意味着不在官方记录内",prio=60,group="cus_reply",tags=["main"])
line("DL_CUS_CUSTOMS_D2_003","customs_officer","customs_house",2,"morning","choice_response",
     "选项b：玩家索要多佛底单，说明调阅程序与耗时",prio=60,group="cus_reply",tags=["main"])
line("DL_CUS_CUSTOMS_D3_004","customs_officer","customs_house",3,"morning","enter_scene",
     "D3交出多佛底单，附老管家的手写备注",event="D3_action_dover",prio=80,visit=2,
     eff={"addItems":["dover_copy"],"setFlags":["main.dover_marginalia"]},tags=["main"])
line("DL_CUS_CUSTOMS_D2_005","customs_officer","customs_house",2,None,"idle_fallback",
     "兜底：按规程办事",prio=10,once=False,tags=["fallback"])
line("DL_JDG_COURT_D7_001","judge","court",7,"morning","forced_node",
     "开庭程序性宣告",event="F3_court",prio=100,tags=["main","F3"])
line("DL_JDG_COURT_D7_002","judge","court",7,"morning","npc_initiative",
     "变体：庭审证据链完整，驳回辩方主张，准予羁押",event="F3_court",prio=45,group="judge_verdict",
     req=("main.wife_letter","main.manager_confessed"),
     eff={"setFlags":["main.lawyer_convicted"]},tags=["variant","F3"])
line("DL_JDG_COURT_D7_003","judge","court",7,"morning","npc_initiative",
     "变体：证据不足，允许保释但限制出境（剧本原结局，同组兜底）",event="F3_court",prio=40,
     group="judge_verdict",eff={"setFlags":["main.lawyer_bailed"]},tags=["variant","F3","fail_branch"])
line("DL_JDG_COURT_D7_004","judge","court",7,"morning","forced_node",
     "传唤证人作证",event="F3_court",prio=100,tags=["main","F3"])
line("DL_OFC_POLICE_D7_001","police_officer","police_station",7,"afternoon","forced_node",
     "报告七橡树镇之行，转交铁盒",event="D7_action_police",prio=100,tags=["main"])
line("DL_OFC_POLICE_D7_002","police_officer","police_station",7,"afternoon","forced_node",
     "描述阁楼搜查经过与铁盒内十二封信",event="D7_action_police",prio=100,tags=["main"])
line("DL_OFC_POLICE_D5_003","police_officer","police_station",5,"morning","npc_initiative",
     "出发前的简短确认",prio=30,req=("main.know_kent_address",),tags=["variant"])

# ===== 8.11 物证文书 (18) =====
line("DOC_WHARF_D1_001","narrator","wharf_12",1,"night","item_inspect",
     "旧门房背面新贴纸条：明日午夜，老地方",event="D1_action3_night",prio=100,typ="document",
     render="handwritten_letter",eff={"setFlags":["side.midnight_note"],"addItems":["midnight_note"]},tags=["side"])
line("DOC_WHARF_D1_002","narrator","wharf_12",1,"morning","item_inspect",
     "登记簿：M-412号货箱18日18:30由码头经理签收，转运签收栏空白",prio=100,typ="document",
     render="official_record",tags=["main"])
line("DOC_TAVERN_D2_003","landlady","tavern",2,"dusk","item_inspect",
     "匿名纸条：粉石头的交接点在皇后公园站",prio=100,typ="document",render="handwritten_letter",tags=["main"])
line("DOC_CUSTOMS_D2_004","narrator","customs_house",2,"morning","item_inspect",
     "报关单：随附私人信物一件，不计价",prio=100,typ="document",render="official_record",tags=["main"])
line("DOC_MGOFF_D3_005","narrator","manager_office",3,"morning","item_inspect",
     "打字机短函：确认货物含粉红色物件，优先取出，其余按原计划替换",event="D3_action_mgoff",prio=100,
     typ="document",render="typed_document",eff={"setFlags":["main.typed_memo"],"addItems":["typed_memo"]},tags=["main"])
line("DOC_MGOFF_D3_006","narrator","manager_office",3,"morning","item_inspect",
     "台历：5月18日被圈两圈，旁注N.O.",event="D3_action_mgoff",prio=100,typ="document",
     render="handwritten_letter",eff={"setFlags":["main.calendar_n_o"],"addItems":["desk_calendar"]},tags=["main"])
line("DOC_MGOFF_D3_007","narrator","manager_office",3,"morning","item_inspect",
     "木板边缘被擦掉的墨迹残痕K.-e-…",prio=100,typ="document",render="handwritten_letter",item="ink_trace_board",
     eff={"setFlags":["side.ink_trace"],"addItems":["ink_trace_board"]},tags=["side"])
line("DOC_CUSTOMS_D3_008","old_butler","customs_house",3,"morning","item_inspect",
     "多佛底单老管家手写备注：圆盒包装、外包红绸、随货同行、不列清单",event="D3_action_dover",prio=100,
     typ="document",render="handwritten_letter",tags=["main"])
line("DOC_NEWS_D3_009","editor","newsroom",3,"afternoon","item_inspect",
     "主编桌上写了一半的信，收信人是律师",event="D3_action_news",prio=100,typ="document",
     render="handwritten_letter",item="half_written_letter",
     eff={"setFlags":["side.letter_half"],"addItems":["half_written_letter"]},tags=["hidden"])
line("DOC_MANSION_D4_010","young_noble","mansion_study",4,"afternoon","item_inspect",
     "钻石档案",event="F2_mansion_photo",prio=100,typ="document",render="official_record",tags=["main","F2"])
line("DOC_MANSION_D4_011","young_noble","mansion_study",4,"afternoon","item_inspect",
     "银质封印戒指与授信函（道具描述文本）",event="F2_mansion_photo",prio=100,typ="document",
     render="official_record",tags=["main","F2"])
line("DOC_MANSION_D4_012","narrator","mansion_study",4,"afternoon","item_inspect",
     "旧银版照片：深棕色卷发、面容温婉的年轻女子",event="F2_mansion_photo",prio=100,typ="document",
     render="official_record",eff={"setFlags":["hidden.face_resemblance"]},tags=["side_origin","F2"])
line("DOC_WIF_HOTEL_D5_005","manager_wife","hotel_swan",5,"morning","item_inspect",
     "经理妻子的信（索引占位，正文见8.7三条）",prio=20,typ="document",item="wife_letter",
     render="handwritten_letter",tags=["main"])
line("DOC_TUNNEL_D5_014","narrator","tunnel",5,"night","item_inspect",
     "空木盒：内衬深红锦缎、中央圆形凹痕，与货箱压痕一致",event="D5_action_tunnel",prio=100,typ="document",
     eff={"setFlags":["main.empty_box"],"addItems":["empty_wood_box"]},tags=["main"])
line("DOC_TUNNEL_D5_015","narrator","tunnel",5,"night","item_inspect",
     "盒盖缝隙里的深棕色卷曲发缕",event="D5_action_tunnel",prio=100,typ="document",
     eff={"setFlags":["hidden.hair_sample"],"addItems":["brown_hair_lock"]},tags=["main","side_origin"])
line("DOC_MANSION_D6_016","young_noble","mansion_study",6,"afternoon","item_inspect",
     "侍女雇佣记录：艾琳·哈珀，1882-83任侍女，未婚无亲属，父辈补注1884年生育男婴",
     event="D6_action_return",prio=100,typ="document",render="official_record",
     eff={"setFlags":["hidden.mother_named","hidden.birth_year_gap"],"addItems":["maid_record"]},tags=["side_origin"])
line("DOC_NEWS_D6_017","editor","newsroom",6,"dusk","item_inspect",
     "桌上信件一行字：律师已付清。——R.",event="D6_action_news",prio=100,typ="document",
     render="handwritten_letter",eff={"setFlags":["hidden.editor_bribe"],"addItems":["paid_note"]},tags=["hidden"])
line("DOC_POLICE_D7_018","narrator","police_station",7,"afternoon","item_inspect",
     "十二封往来信函，重点两封（二月：九月前完成基金归属申请；五月十二日：十八日到港，夹层内红绸包裹物优先取出）",
     event="D7_action_police",prio=100,typ="document",render="typed_document",
     eff={"setFlags":["main.twelve_letters"],"addItems":["iron_box_letters"]},tags=["main"])

# ===== 旁白结局 (4) =====
line("NAR_NEWS_D7_090","narrator","newsroom",7,"midnight","ending",
     "深夜写完报道，标题《东方之星：一颗石头的去向》",event="D7_action_final",prio=100,typ="narration",
     render="narration_box",eff={"addItems":["final_manuscript"]},tags=["ending"])
line("NAR_NEWS_D7_091","narrator","newsroom",7,"midnight","ending",
     "把照片与发缕装进信封、锁进抽屉",event="D7_action_final",prio=100,typ="narration",
     render="narration_box",req=("hidden.photo_acquired","hidden.hair_sample"),tags=["ending","side_origin"])
line("NAR_NEWS_D7_092","narrator","newsroom",7,"midnight","ending",
     "打开新本子写下两行字：主编与律师有资金往来、动机不明；侍女在七橡树镇磨坊巷3号，她可能在那里",
     event="D7_action_final",prio=100,typ="narration",render="narration_box",tags=["ending","hidden"])
line("NAR_NEWS_D7_093","narrator","newsroom",7,"midnight","ending",
     "收尾：窗外东区的夜雾正在升起",event="D7_action_final",prio=100,typ="narration",
     render="narration_box",tags=["ending"])

# ---------------------------------------------------------------- 决策节点
CN = []
def node(id, day, slot, loc, event, prompt, opts, maxpicks=1, allow_skip=False,
         repeatable=False, on_exhausted=None, skip_line=None):
    CN.append(dict(id=id, day=day, slot=slot, location=loc, event=event, promptLine=prompt,
                   options=opts, maxPicks=maxpicks, allowSkip=allow_skip,
                   repeatable=repeatable, onExhausted=on_exhausted, skipLine=skip_line))

def opt(oid, summary, resp=None, req=(), eff=None, hidden=False):
    return dict(id=oid, label="", summary=summary,
                requires={"requiresFlags": list(req)} if req else {},
                hidden=hidden, responseLines=resp or [], effects=eff or {})

node("CN_D1_WHARF_PROLOGUE",1,"dawn","wharf_12","prologue_wharf","DL_NOB_WHARF_D1_001",
     [opt("a","指出锁扣上的弧形压痕是羊角锤留下的",["DL_NOB_WHARF_D1_002"]),
      opt("b","观察对方穿着——靴子手艺与大衣纽扣暗纹",["DL_NOB_WHARF_D1_003"]),
      opt("c","直接报出《东区观察家报》记者身份",["DL_NOB_WHARF_D1_004"]),
      opt("d","沉默不语，等他先开口",["DL_NOB_WHARF_D1_005"])],
     on_exhausted="DL_NOB_WHARF_D1_006")

node("CN_D1_WHARF_EVIDENCE",1,"morning","wharf_12","D1_action1_search","DL_DET_WHARF_D1_003",
     [opt("a","起开底板检查夹层：深红锦缎、圆形压痕、粉红晶屑",["DL_DET_WHARF_D1_005"]),
      opt("b","沿地面拖痕追往冷藏库方向，取水泥裂缝中的织物纤维",["DL_DET_WHARF_D1_006"]),
      opt("c","找码头工人问昨晚口供",["DL_DET_WHARF_D1_007","DL_WKR_WHARF_D1_001"]),
      opt("d","翻查货物登记簿 M-412 号签收记录",["DL_DET_WHARF_D1_008"])],
     maxpicks=2, repeatable=True, on_exhausted="DL_DET_WHARF_D1_009")

node("CN_D1_WHARF_WORKER",1,"morning","wharf_12","D1_action1_search",None,
     [opt("a","追问油布木箱的尺寸与重量感",["DL_WKR_WHARF_D1_003"]),
      opt("b","确认经理当晚是否独自一人",["DL_WKR_WHARF_D1_004"]),
      opt("c","问及当晚还有谁在场",["DL_WKR_WHARF_D1_005"]),
      opt("d","结束询问",[])])

node("CN_D1_NEWS_EDITOR",1,"afternoon","newsroom","D1_action2_news","DL_EDT_NEWS_D1_002",
     [opt("a","如实报出托运方与保额",["DL_EDT_NEWS_D1_003"]),
      opt("b","反问为何更关心保额而非谁偷的",["DL_EDT_NEWS_D1_004"]),
      opt("c","提及他桌上那封来自律师的信",["DL_EDT_NEWS_D1_005"]),
      opt("d","隐瞒发现，只报常规进展",["DL_EDT_NEWS_D1_006"])])

node("CN_D2_CUSTOMS",2,"morning","customs_house","D2_action_customs","DL_CUS_CUSTOMS_D2_001",
     [opt("a","追问不计价在实务上的含义",["DL_CUS_CUSTOMS_D2_002"]),
      opt("b","索要多佛底单",["DL_CUS_CUSTOMS_D2_003"]),
      opt("c","出示记者证要求查阅完整报关档案",[]),
      opt("d","记下信息后离开",[])])

node("CN_D2_TAVERN_NOTE",2,"dusk","tavern","D2_action_tavern","DL_LND_TAVERN_D2_021",
     [opt("a","追问匿名纸条来路",["DL_LND_TAVERN_D2_022"]),
      opt("b","询问皇后公园站与储物柜编号",["DL_LND_TAVERN_D2_023"]),
      opt("c","用警探的马车情报交换更多消息",["DL_LND_TAVERN_D2_024"],req=("main.black_carriage",)),
      opt("d","质疑她为何愿意帮忙",["DL_LND_TAVERN_D2_025"])])

node("CN_D2_POLICE_TALK",2,"morning","police_station","D2_action_police","DL_DET_POLICE_D2_021",
     [opt("a","追问经理的作案动机",["DL_DET_POLICE_D2_022"]),
      opt("b","提及海关不计价记录",["DL_DET_POLICE_D2_023"],req=("main.customs_unvalued",)),
      opt("c","出示酒馆匿名纸条",["DL_DET_POLICE_D2_024"],req=("main.anon_note",)),
      opt("d","保留情报，什么都不说",["DL_DET_POLICE_D2_025"])])

node("CN_D3_MGOFF_SEARCH",3,"morning","manager_office","D3_action_mgoff","DL_MGR_MGOFF_D3_003",
     [opt("a","翻查抽屉底板夹缝",["DOC_MGOFF_D3_005"]),
      opt("b","检查桌上台历",["DOC_MGOFF_D3_006"]),
      opt("c","取走办公室钥匙",[],eff={"addItems":["office_key"],"setFlags":["side.cold_storage_key"]}),
      opt("d","记下环境细节后离开",[])],
     maxpicks=2, repeatable=True)

node("CN_D3_WHARF_RECHECK",3,"morning","wharf_12","D3_action_recheck",None,
     [opt("a","翻起底板背面再次取样",[]),
      opt("b","检查木板边缘被擦掉的墨迹残痕",["DOC_MGOFF_D3_007"]),
      opt("c","与搬运工闲谈",["DL_WKR_WHARF_D3_020"]),
      opt("d","离开",[])])

node("CN_D3_CUSTOMS_DOVER",3,"morning","customs_house","D3_action_dover","DL_CUS_CUSTOMS_D3_004",
     [opt("a","细读老管家的手写备注",["DOC_CUSTOMS_D3_008"]),
      opt("b","询问老管家的身份与在府年限",[]),
      opt("c","请求复制底单存档",[]),
      opt("d","离开",[])])

node("CN_D3_NEWS_LETTER",3,"afternoon","newsroom","D3_action_news","DL_EDT_NEWS_D3_010",
     [opt("a","直接问及那封写给律师的信",["DL_EDT_NEWS_D3_011"]),
      opt("b","假意只看稿子，暗中记下信封细节",["DOC_NEWS_D3_009"]),
      opt("c","借口赶稿离开",[]),
      opt("d","试探性提起律师的名字",[])])

node("CN_D4_LAWOFF_CLERK",4,"morning","lawyer_office","D4_action_lawoff","DL_CLK_LAWOFF_D4_001",
     [opt("a","追问确切出差日期",["DL_CLK_LAWOFF_D4_002"]),
      opt("b","询问近期访客",["DL_CLK_LAWOFF_D4_003"]),
      opt("c","出示记者证与授信函施压",["DL_CLK_LAWOFF_D4_004"],req=("main.seal_ring",)),
      opt("d","留下名片请她转交",["DL_CLK_LAWOFF_D4_005"])])

node("CN_D4_TAVERN_WIFE",4,"dusk","tavern","D4_action_tavern","DL_LND_TAVERN_D4_031",
     [opt("a","追问皮箱大小与内容猜测",["DL_LND_TAVERN_D4_032"]),
      opt("b","询问是谁替她叫的马车",["DL_LND_TAVERN_D4_033"]),
      opt("c","问及经理的欠债",["DL_LND_TAVERN_D4_034"]),
      opt("d","问及律师是否常来酒馆",["DL_LND_TAVERN_D4_035"])])

node("CN_D4_MANSION_TALK",4,"afternoon","mansion_study","F2_mansion_photo","DL_NOB_MANSION_D4_015",
     [opt("a","汇报调查进展",["DL_NOB_MANSION_D4_016"]),
      opt("b","询问东方之星的背景与信托条款",["DL_NOB_MANSION_D4_017"]),
      opt("c","提及压在书下的照片女子",["DL_NOB_MANSION_D4_019"]),
      opt("d","直接开口借走那张照片",["DL_NOB_MANSION_D4_020","DL_NOB_MANSION_D4_021"])])

node("CN_D4_HOTEL_WIFE",4,"night","hotel_swan","F2_mansion_photo","DL_WIF_HOTEL_D4_010",
     [opt("a","追问往来信函的藏匿位置",["DL_WIF_HOTEL_D4_011"]),
      opt("b","询问是否见过戴面纱的女子",["DL_WIF_HOTEL_D4_012"]),
      opt("c","先承诺保护她的安全，再请她出庭作证",["DL_WIF_HOTEL_D4_013"],
          eff={"setFlags":["hidden.wife_protected","hidden.wife_testified"]}),
      opt("d","不做承诺，直接要求她出庭作证",["DL_WIF_HOTEL_D4_014"],
          eff={"npcState":{"manager_wife":{"fled":True}}})],
     on_exhausted=None)

node("CN_D5_HOTEL_LETTER",5,"morning","hotel_swan","D5_action_hotel","DL_WIF_HOTEL_D5_001",
     [opt("a","向柜台打听她的去向",[]),
      opt("b","取走留下的信",["DOC_WIF_HOTEL_D5_002"]),
      opt("c","查看房间是否还有遗留物",[]),
      opt("d","离开旅馆",[])])

node("CN_D5_POLICE_TALK",5,"morning","police_station","D5_action_police","DL_DET_POLICE_D5_041",
     [opt("a","追问主编的可疑之处",["DL_DET_POLICE_D5_043"]),
      opt("b","请求派人保护经理妻子",["DL_DET_POLICE_D5_044"]),
      opt("c","隐瞒照片与发缕，只交出信",["DL_DET_POLICE_D5_045"],req=("hidden.hair_sample",)),
      opt("d","提出隧道与空木盒，请他带人搜查",["DL_DET_POLICE_D5_046"],req=("main.empty_box",))])

node("CN_D5_TUNNEL",5,"night","tunnel","D5_action_tunnel",None,
     [opt("a","用经理办公室钥匙打开铁门",[],req=("side.cold_storage_key",)),
      opt("b","在废弃货架后寻找向下的隧道",["DOC_TUNNEL_D5_014"],
          eff={"setFlags":["side.tunnel_found"]}),
      opt("c","检查木盒盒盖缝隙",["DOC_TUNNEL_D5_015"]),
      opt("d","原路返回",[])],
     maxpicks=2, repeatable=True)

node("CN_D6_STATION_LOCKER",6,"morning","park_station","D6_action_station",None,
     [opt("a","按纸条编号打开17号储物柜",[],req=("main.anon_note",),
          eff={"addItems":["eastern_star"],"setFlags":["main.diamond_recovered"]}),
      opt("b","先观察四周有无可疑人",[]),
      opt("c","向站务员询问近期取件人",[]),
      opt("d","放弃，直接去宅邸",[])])

node("CN_D6_MANSION_RETURN",6,"afternoon","mansion_study","D6_action_return","DL_NOB_MANSION_D6_030",
     [opt("a","问他是否替自己查过照片上的女子",["DL_NOB_MANSION_D6_031"]),
      opt("b","沉默，或说出自己的出生年份",["DL_NOB_MANSION_D6_034"]),
      opt("c","把记录推回去，表示不要",["DL_NOB_MANSION_D6_035"]),
      opt("d","要求继续追查七橡树镇",["DL_NOB_MANSION_D6_036"])])

node("CN_D6_POLICE_MANAGER",6,"morning","police_station","D6_action_return",None,
     [opt("a","追问戴面纱女性的身份",["DL_MGR_POLICE_D6_012"]),
      opt("b","出示其妻的信",["DL_MGR_POLICE_D6_013"],req=("main.wife_letter",)),
      opt("c","提及铅块替换，逼他补出细节",["DL_MGR_POLICE_D6_014"],req=("main.typed_memo",)),
      opt("d","承诺不写他妻子",["DL_MGR_POLICE_D6_015"])])

node("CN_D1_WHARF_GAP",1,"morning","wharf_12","D1_action1_search","DL_DET_WHARF_D1_001",
     [opt("a","蹲下检查，指出底板与侧板之间缝隙的木刺断裂",["DL_DET_WHARF_D1_002","DL_DET_WHARF_D1_003"],
          eff={"relationship":{"detective":1}}),
      opt("b","先拍照，不开口提醒他",[],eff={"relationship":{"detective":-1}}),
      opt("c","直接问他对失窃的判断",["DL_DET_WHARF_D1_002"]),
      opt("d","出示记者证要求参与勘查",[])],
     on_exhausted="DL_DET_WHARF_D1_004")

node("CN_D1_TAVERN_INTEL",1,"dusk","tavern","F1_tavern_reveal","DL_LND_TAVERN_D1_004",
     [opt("a","追问夹层细节",["DL_LND_TAVERN_D1_005"]),
      opt("b","询问老管家身份",["DL_LND_TAVERN_D1_006"]),
      opt("c","表明自己不会乱写",["DL_LND_TAVERN_D1_007"]),
      opt("d","追问她的消息来源",["DL_LND_TAVERN_D1_008"])],
     on_exhausted=None)

node("CN_D5_NEWS_EDITOR",5,"afternoon","newsroom","D5_action_police","DL_EDT_NEWS_D5_020",
     [opt("a","如实说明进展",["DL_EDT_NEWS_D5_021"]),
      opt("b","敷衍过去，只说还在查",["DL_EDT_NEWS_D5_021"]),
      opt("c","反问他为何打听",["DL_EDT_NEWS_D5_021"]),
      opt("d","借口赶稿离开",[])])

node("CN_D6_POLICE_TALK",6,"morning","police_station","D6_action_return","DL_DET_POLICE_D6_074",
     [opt("a","要求查阅经理供词全文",["DL_DET_POLICE_D6_075"]),
      opt("b","追问戴面纱女性的身份推测",["DL_DET_POLICE_D6_076"],req=("main.veil_woman",)),
      opt("c","询问肯特郡取证的进度",[]),
      opt("d","离开警局",[])])

node("CN_D6_POLICE_MANAGER_DETAIL",6,"morning","police_station","D6_action_return",None,
     [opt("a","追问台历上5月18日的两个圈与N.O.的含义",["DL_MGR_POLICE_D6_016"],req=("main.calendar_n_o",)),
      opt("b","追问打字机短函是谁写给他的",["DL_MGR_POLICE_D6_014"],req=("main.typed_memo",)),
      opt("c","追问妻子是否知情",["DL_MGR_POLICE_D6_013"],req=("main.wife_letter",)),
      opt("d","结束审讯",[])])

node("CN_D7_POLICE_MANAGER",7,"morning","police_station","D7_action_police","DL_DET_POLICE_D7_060",
     [opt("a","追问经理认罪后的量刑可能",[]),
      opt("b","确认肯特郡取回的十二封信已入卷",["DL_DET_POLICE_D7_061"]),
      opt("c","请求查阅两封关键信函的原件",["DL_DET_POLICE_D7_061"]),
      opt("d","离开警局去报社",[])])

node("CN_D7_COURT_EVIDENCE",7,"morning","court","F3_court","DL_JDG_COURT_D7_004",
     [opt("a","出示码头经理妻子的信",["DL_LAW_COURT_D7_012","DL_DET_COURT_D7_051"],req=("main.wife_letter",)),
      opt("b","指出律师前妻改嫁入旁支、曾为府中婢女",["DL_LAW_COURT_D7_013"],req=("main.veil_woman",),
          eff={"setFlags":["main.ex_wife_identity"]}),
      opt("c","提及已付清字条与主编的资金往来",["DL_LAW_COURT_D7_014"],req=("hidden.editor_bribe",)),
      opt("d","宣读码头经理的完整供词",["DL_LAW_COURT_D7_015","DL_LAW_COURT_D7_016"],req=("main.manager_confessed",))],
     maxpicks=2, repeatable=True, allow_skip=True, skip_line="DL_DET_COURT_D7_052",
     on_exhausted="DL_LAW_COURT_D7_033")

node("CN_D7_COURT_CORRIDOR",7,"afternoon","court_corridor","F3_court",None,
     [opt("a","低声质问：已付清的那个人是您吧",["DL_LAW_CORRIDOR_D7_030"]),
      opt("b","沉默让他走过",["DL_LAW_CORRIDOR_D7_031"]),
      opt("c","提及旁支与信托基金",["DL_LAW_CORRIDOR_D7_032"]),
      opt("d","转身离开",[])])

node("CN_D7_NEWS_FINAL",7,"midnight","newsroom","D7_action_final","NAR_NEWS_D7_090",
     [opt("a","在新本子上写下主编的可疑记录",["DL_EDT_NEWS_D7_031"],req=("hidden.editor_bribe",)),
      opt("b","沉默交稿，不提任何疑点",["DL_EDT_NEWS_D7_032"]),
      opt("c","提及七橡树镇与那位侍女",["DL_EDT_NEWS_D7_033"],req=("main.know_kent_address",)),
      opt("d","把照片与发缕锁进抽屉",["NAR_NEWS_D7_091"],req=("hidden.photo_acquired",))])

# ---------------------------------------------------------------- 组装输出
def build_line(x):
    d = collections.OrderedDict()
    d["id"] = x["id"]
    d["type"] = x["typ"]
    d["version"] = "1.0"
    d["npc"] = x["npc"]
    d["target"] = x["target"]
    d["location"] = x["loc"]
    t = {"day": x["day"], "slot": [x["slot"]] if x["slot"] else ["any"],
         "weather": x.get("weather"), "visitIndex": x["visit"]}
    d["time"] = t
    d["event"] = x["event"]
    tr = {"type": x["trigger"], "nodeId": None, "choiceNodeId": None,
          "optionId": None, "itemId": x.get("item"), "eventId": x["event"]}
    if x["trigger"] == "choice_response":
        tr["choiceNodeId"] = None
        tr["optionId"] = None
    d["trigger"] = tr
    cond = {}
    if x["req"]:  cond["requiresFlags"] = x["req"]
    if x["forb"]: cond["forbidsFlags"] = x["forb"]
    if x["items"]:cond["requiresItems"] = x["items"]
    if x["rel"]:  cond["relationship"] = x["rel"]
    if x["chain"]:cond["chain"] = x["chain"]
    if x["budget"]:cond["actionBudget"] = x["budget"]
    if x["visit"] is not None: cond["visitIndex"] = {"==": x["visit"]}
    d["conditions"] = cond
    d["priority"] = x["prio"]
    d["once"] = x["once"]
    d["group"] = x["group"]
    d["text"] = ""
    d["summary"] = x["summary"]
    dl = {"emotion": x["emotion"], "tone": x["tone"], "action": x["action"],
          "sfx": None, "bgm": None, "portrait": None, "render": x["render"], "camera": None}
    d["delivery"] = dl
    d["effects"] = x["eff"]
    d["next"] = x["nxt"] or {"type": "auto", "id": None}
    d["fallback"] = x["fallback"]
    d["tags"] = x["tags"]
    d["notes"] = ""
    return d

# 回填 item_inspect 的 itemId：优先用 enums.item.inspectLine 的反向映射，
# 其次用该条 effects.addItems 的首个道具
inspect_map = {i[3]: i[0] for i in ITEMS if i[3]}
for x in L:
    if x["trigger"] == "item_inspect":
        x["item"] = (x.get("item")                     # 1. 显式指定优先
                     or inspect_map.get(x["id"])       # 2. 道具表的 inspectLine 反查
                     or (x["eff"].get("addItems") or [None])[0])  # 3. 该条产出的首个道具

# 回填 choice_response 的 choiceNodeId / optionId
opt_map = {}
for n in CN:
    for o in n["options"]:
        for rid in o["responseLines"]:
            opt_map.setdefault(rid, []).append((n["id"], o["id"]))

built = []
for x in L:
    b = build_line(x)
    if x["trigger"] == "choice_response" and x["id"] in opt_map:
        pairs = opt_map[x["id"]]
        b["trigger"]["choiceNodeId"] = pairs[0][0]
        b["trigger"]["optionId"] = pairs[0][1]
        if len(pairs) > 1:
            b["notes"] = "被多个节点引用：" + ", ".join(f"{p[0]}#{p[1]}" for p in pairs[1:])
    built.append(b)

bank = collections.OrderedDict()
bank["meta"] = {
    "title": "港口宝石失窃案",
    "schemaVersion": "1.0",
    "scriptVersion": "粗略剧本·身份版",
    "setting": {"era": "1896年5月", "location": "伦敦·东区码头",
                "days": 7, "actionsPerDay": 3},
    "playerInteraction": "a/b/c/d 选项推进；部分决策节点支持多选（见 choiceNodes[].maxPicks）",
    "note": "所有 text 字段留白，由文案按 summary 填写。台词正文与结构分离，可在 text 全空时跑通全流程做逻辑验证。"
}
bank["enums"] = {
    "npc": [dict(id=n[0], code=n[1], role=n[2], displayName=n[3], age=n[4],
                 speechTraits=n[5], verbalTic=n[6], locations=n[7], stateMachine=n[8]) for n in NPCS],
    "location": [dict(id=l[0], code=l[1], name=l[2], function=l[3],
                      enterable=True, revisitable=True) for l in LOCS],
    "timeSlot": ["dawn","morning","noon","afternoon","dusk","night","midnight","any"],
    "event": [dict(id=e[0], day=e[1], location=e[2], forced=e[3], skipCost=None) for e in EVENTS],
    "flag": [dict(id=f[0], namespace=f[1], desc=f[2],
                  irreversible=(f[1] == "hidden"), setBy=[]) for f in FLAGS],
    "item": [dict(id=i[0], name=i[1], kind=i[2], inspectLine=i[3],
                  chainRelevance=i[4]) for i in ITEMS],
}
bank["lines"] = built
bank["choiceNodes"] = CN
bank["endings"] = [
    {"id":"END_CANON","name":"结案·钻石归还、律师被羁押、经理认罪",
     "conditions":{"requiresFlags":["main.diamond_recovered","main.lawyer_convicted"]},
     "lines":["NAR_NEWS_D7_090","NAR_NEWS_D7_091","NAR_NEWS_D7_092","NAR_NEWS_D7_093"],"isCanon":True},
    {"id":"END_BAIL","name":"保释·限制出境（剧本原结局）",
     "conditions":{"requiresFlags":["main.diamond_recovered","main.lawyer_bailed"]},
     "lines":["DL_JDG_COURT_D7_003","DL_LAW_COURT_D7_020","NAR_NEWS_D7_090","NAR_NEWS_D7_092","NAR_NEWS_D7_093"],
     "isCanon":True},
    {"id":"END_LOSS","name":"失败·钻石未追回",
     "conditions":{"forbidsFlags":["main.diamond_recovered"]},
     "lines":["DL_LAW_COURT_D7_017","DL_NOB_MANSION_D6_040","NAR_NEWS_D7_093"],"isCanon":False},
    {"id":"END_WIFE_TESTIFY","name":"支线·经理妻子出庭作证",
     "conditions":{"requiresFlags":["hidden.wife_testified","main.diamond_recovered"]},
     "lines":["DL_WIF_COURT_D7_020","NAR_NEWS_D7_090","NAR_NEWS_D7_092"],"isCanon":False},
]

with open(OUT, "w", encoding="utf-8") as f:
    json.dump(bank, f, ensure_ascii=False, indent=2)

# ---------------------------------------------------------------- 统计校验
ids = [b["id"] for b in built]
dup = [k for k, v in collections.Counter(ids).items() if v > 1]
per_npc = collections.Counter(x["npc"] for x in built)
print("写入:", OUT)
print("台词/文书/旁白条数:", len(built))
print("决策节点数:", len(CN))
print("选项总数:", sum(len(n["options"]) for n in CN))
print("重复ID:", dup or "无")
print("枚举: NPC", len(NPCS), "地点", len(LOCS), "事件", len(EVENTS), "flag", len(FLAGS), "道具", len(ITEMS))
print("按NPC分布:")
for k, v in sorted(per_npc.items(), key=lambda p: -p[1]):
    print(f"  {k:16s} {v}")
# 引用完整性
all_ids = set(ids)
bad = []
for n in CN:
    if n["promptLine"] and n["promptLine"] not in all_ids: bad.append((n["id"],"prompt",n["promptLine"]))
    if n["onExhausted"] and n["onExhausted"] not in all_ids: bad.append((n["id"],"exhaust",n["onExhausted"]))
    for o in n["options"]:
        for r in o["responseLines"]:
            if r not in all_ids: bad.append((n["id"],o["id"],r))
for e in bank["endings"]:
    for r in e["lines"]:
        if r not in all_ids: bad.append((e["id"],"ending",r))
print("悬空引用:", bad or "无")

# flag / item / npc / location 登记完整性校验
flag_ids = {f[0] for f in FLAGS}
item_ids = {i[0] for i in ITEMS}
npc_ids = {n[0] for n in NPCS}
loc_ids = {l[0] for l in LOCS}
evt_ids = {e[0] for e in EVENTS}
miss = collections.defaultdict(list)
for b in built:
    c = b["conditions"]
    for f in c.get("requiresFlags", []) + c.get("forbidsFlags", []):
        if f not in flag_ids: miss["flag"].append((b["id"], f))
    for it in c.get("requiresItems", []) + c.get("forbidsItems", []):
        if it not in item_ids: miss["item"].append((b["id"], it))
    e = b["effects"]
    for f in e.get("setFlags", []) + e.get("clearFlags", []):
        if f not in flag_ids: miss["flag"].append((b["id"], f))
    for it in e.get("addItems", []) + e.get("removeItems", []):
        if it not in item_ids: miss["item"].append((b["id"], it))
    for k in e.get("relationship", {}):
        if k not in npc_ids: miss["npc"].append((b["id"], k))
    if b["npc"] not in npc_ids: miss["npc"].append((b["id"], b["npc"]))
    if b["location"] not in loc_ids: miss["location"].append((b["id"], b["location"]))
    if b["event"] and b["event"] not in evt_ids: miss["event"].append((b["id"], b["event"]))
for n in CN:
    if n["location"] not in loc_ids: miss["location"].append((n["id"], n["location"]))
    if n["event"] and n["event"] not in evt_ids: miss["event"].append((n["id"], n["event"]))
    for o in n["options"]:
        for f in o["requires"].get("requiresFlags", []):
            if f not in flag_ids: miss["flag"].append((f"{n['id']}#{o['id']}", f))
        for f in o["effects"].get("setFlags", []):
            if f not in flag_ids: miss["flag"].append((f"{n['id']}#{o['id']}", f))
        for it in o["effects"].get("addItems", []):
            if it not in item_ids: miss["item"].append((f"{n['id']}#{o['id']}", it))
for k, v in miss.items():
    print(f"未登记 {k}:", sorted(set(v)) or "无")
if not miss:
    print("未登记引用: 无")

# setFlags 但从未被任何 requiresFlags 使用的 flag（提示冗余）
used = set()
for b in built:
    c = b["conditions"]
    used.update(c.get("requiresFlags", []))
    used.update(c.get("forbidsFlags", []))
for n in CN:
    for o in n["options"]:
        used.update(o["requires"].get("requiresFlags", []))
for e in bank["endings"]:
    used.update(e["conditions"].get("requiresFlags", []))
    used.update(e["conditions"].get("forbidsFlags", []))
setflags = set()
for b in built:
    setflags.update(b["effects"].get("setFlags", []))
for n in CN:
    for o in n["options"]:
        setflags.update(o["effects"].get("setFlags", []))
orphan = sorted(f for f in flag_ids if f not in used and not f.startswith("sys."))
print("已登记但从未被条件引用的 flag（可精简或留作扩展）:", len(orphan))
print("  ", orphan[:12], "…" if len(orphan) > 12 else "")
never_set = sorted(f for f in used if f not in setflags and not f.startswith("sys."))
print("被条件引用但从未被任何环节置位的 flag（永远为假，需检查）:", never_set or "无")

# ---------------------------------------------------------------- 全量清单（从数据自动生成）
LIST_OUT = "/Users/ouyang2005/Documents/qwen-agent/XZs3TnbjzR/default/台词库清单-全量.md"
disp = {n["id"]: n["displayName"] for n in bank["enums"]["npc"]}
locn = {l["id"]: l["name"] for l in bank["enums"]["location"]}
TRIG = {"forced_node": "强制节点", "choice_response": "选项回应", "enter_scene": "进场",
        "item_inspect": "查看物证", "npc_initiative": "状态变体", "idle_fallback": "兜底",
        "ending": "结局"}

def fmt_cond(c):
    p = []
    if c.get("requiresFlags"): p.append("需 " + "、".join(f"`{f}`" for f in c["requiresFlags"]))
    if c.get("forbidsFlags"):  p.append("禁 " + "、".join(f"`{f}`" for f in c["forbidsFlags"]))
    if c.get("requiresItems"): p.append("持有 " + "、".join(f"`{i}`" for i in c["requiresItems"]))
    if c.get("relationship"):
        p.append("关系 " + "；".join(f"{disp.get(k,k)} {json.dumps(v,ensure_ascii=False)}" for k,v in c["relationship"].items()))
    if c.get("visitIndex"): p.append(f"第{c['visitIndex'].get('==','?')}次访问")
    if c.get("chain"):      p.append(f"选择链 mode={c['chain']['mode']}")
    return "；".join(p) or "—"

def fmt_eff(e):
    p = []
    if e.get("setFlags"):  p.append("置 " + "、".join(f"`{f}`" for f in e["setFlags"]))
    if e.get("addItems"):  p.append("得 " + "、".join(f"`{i}`" for i in e["addItems"]))
    if e.get("relationship"): p.append("关系" + "、".join(f"{disp.get(k,k)}{'+' if v>0 else ''}{v}" for k,v in e["relationship"].items()))
    if e.get("npcState"):  p.append("改NPC状态")
    if e.get("consumeAction"): p.append("消耗行动")
    return "；".join(p) or "—"

md = []
md.append("# 台词库全量清单（自动生成）\n")
md.append("> 由 `_gen_bank.py` 从 `台词库.json` 直接导出，请勿手工编辑。\n")
md.append(f"> 共 **{len(built)}** 条台词/文书/旁白 · **{len(CN)}** 个决策节点 · **{sum(len(n['options']) for n in CN)}** 个选项\n")
md.append("> `text` 一律留白，文案按「内容简述」填写。\n")

md.append("\n## 一、按 NPC 分组的全量台词\n")
order = sorted({b["npc"] for b in built}, key=lambda k: -per_npc[k])
for npc_id in order:
    rows = [b for b in built if b["npc"] == npc_id]
    tic = next((n["verbalTic"] for n in bank["enums"]["npc"] if n["id"] == npc_id and n.get("verbalTic")), None)
    md.append(f"\n### {disp.get(npc_id, npc_id)} `{npc_id}` — {len(rows)} 条\n")
    if tic:
        md.append(f"> 口癖：句首「{tic}」，由渲染层统一前置，不写进 text。\n")
    md.append("\n| ID | 日/时段 | 地点 | 触发 | 内容简述 | 触发条件 | 效果 |\n|---|---|---|---|---|---|---|\n")
    for b in sorted(rows, key=lambda x: x["id"]):
        t = b["time"]
        d = f"D{t['day']}" if t.get("day") else "—"
        s = t["slot"][0] if t.get("slot") and t["slot"] != ["any"] else "不限"
        md.append("| `{id}` | {d}·{s} | {loc} | {tr} | {sm} | {cd} | {ef} |\n".format(
            id=b["id"], d=d, s=s, loc=locn.get(b["location"], b["location"] or "—"),
            tr=TRIG.get(b["trigger"]["type"], b["trigger"]["type"]),
            sm=b["summary"], cd=fmt_cond(b["conditions"]), ef=fmt_eff(b["effects"])))

md.append("\n## 二、决策节点（a/b/c/d）\n")
for n in CN:
    multi = f"（**多选，最多 {n['maxPicks']} 项**）" if n["maxPicks"] > 1 else ""
    md.append(f"\n### `{n['id']}` — D{n['day']} · {locn.get(n['location'], n['location'])}{multi}\n")
    if n.get("event"): md.append(f"事件：`{n['event']}`\n")
    if n.get("promptLine"): md.append(f"引导台词：`{n['promptLine']}`\n")
    md.append("\n| 选项 | 内容简述 | 前置条件 | 回应台词 | 效果 |\n|---|---|---|---|---|\n")
    for o in n["options"]:
        rq = "、".join(f"`{f}`" for f in o["requires"].get("requiresFlags", [])) or "—"
        rl = "<br>".join(f"`{r}`" for r in o["responseLines"]) or "—"
        md.append(f"| **{o['id']}** | {o['summary']} | {rq} | {rl} | {fmt_eff(o['effects'])} |\n")
    if n.get("onExhausted"): md.append(f"\n选择结束收束：`{n['onExhausted']}`\n")

md.append("\n## 三、结局分支\n")
md.append("\n| ID | 名称 | 条件 | 是否正典 |\n|---|---|---|---|\n")
for e in bank["endings"]:
    md.append(f"| `{e['id']}` | {e['name']} | {fmt_cond(e['conditions'])} | {'是' if e['isCanon'] else '否'} |\n")

md.append("\n## 四、flag 注册表（47 条）\n")
md.append("\n| flag | 命名空间 | 不可逆 | 说明 |\n|---|---|---|---|\n")
for f in bank["enums"]["flag"]:
    md.append(f"| `{f['id']}` | {f['namespace']} | {'是' if f['irreversible'] else ''} | {f['desc']} |\n")

with open(LIST_OUT, "w", encoding="utf-8") as fp:
    fp.write("".join(md))
print("全量清单:", LIST_OUT)


