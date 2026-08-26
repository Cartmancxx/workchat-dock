import Foundation

enum AppCatalog {
    static func defaults() -> [DockApp] {
        [
            app("zalo", "Zalo", "#1677FF", ["com.vng.zalo"], ["/Applications/Zalo.app"]),
            app("dingtalk", "钉钉", "#2F7BFF", ["com.alibaba.DingTalkMac"], ["/Applications/DingTalk.app", "/Applications/钉钉.app"]),
            app("feishu", "飞书 / Lark", "#27C58B", ["com.bytedance.Feishu", "com.bytedance.Lark"], ["/Applications/Feishu.app", "/Applications/Lark.app", "/Applications/飞书.app"]),
            app("jdme", "京ME", "#F53053", [], ["/Applications/京ME.app", "/Applications/JDME.app"]),
            app("wechat", "微信", "#07C160", ["com.tencent.xinWeChat"], ["/Applications/WeChat.app", "/Applications/微信.app"]),
            app("wecom", "企业微信 / WeCom", "#2B83F6", ["com.tencent.WeWorkMac"], ["/Applications/企业微信.app", "/Applications/WeCom.app"]),
            app("qq", "QQ", "#12B7F5", ["com.tencent.qq"], ["/Applications/QQ.app"]),
            app("tencent-meeting", "腾讯会议", "#2D8CFF", ["com.tencent.wemeeting"], ["/Applications/Tencent Meeting.app", "/Applications/腾讯会议.app"]),
            app("teams", "Microsoft Teams", "#6264A7", ["com.microsoft.teams2", "com.microsoft.teams"], ["/Applications/Microsoft Teams.app"]),
            app("slack", "Slack", "#611F69", ["com.tinyspeck.slackmacgap"], ["/Applications/Slack.app"]),
            app("zoom", "Zoom Workplace", "#2D8CFF", ["us.zoom.xos"], ["/Applications/zoom.us.app"]),
            app("webex", "Cisco Webex", "#00BCEB", ["Cisco-Systems.Spark"], ["/Applications/Webex.app"]),
            app("outlook", "Microsoft Outlook", "#1473E6", ["com.microsoft.Outlook"], ["/Applications/Microsoft Outlook.app"]),
            app("notion", "Notion", "#111111", ["notion.id"], ["/Applications/Notion.app"]),
            app("telegram", "Telegram", "#2AABEE", ["ru.keepcoder.Telegram"], ["/Applications/Telegram.app"]),
            app("whatsapp", "WhatsApp", "#25D366", ["net.whatsapp.WhatsApp"], ["/Applications/WhatsApp.app"]),
            app("signal", "Signal", "#3A76F0", ["org.whispersystems.signal-desktop"], ["/Applications/Signal.app"]),
            app("discord", "Discord", "#5865F2", ["com.hnc.Discord"], ["/Applications/Discord.app"]),
            app("line", "LINE", "#06C755", ["jp.naver.line.mac"], ["/Applications/LINE.app"]),
            app("viber", "Viber", "#7360F2", ["com.viber.osx"], ["/Applications/Viber.app"]),
            app("kakaotalk", "KakaoTalk", "#FEE500", ["com.kakao.KakaoTalkMac"], ["/Applications/KakaoTalk.app"]),
            app("mattermost", "Mattermost", "#0058CC", ["com.mattermost.Desktop"], ["/Applications/Mattermost.app"]),
            app("rocket-chat", "Rocket.Chat", "#F5455C", ["chat.rocket"], ["/Applications/Rocket.Chat.app"])
        ]
    }

    private static func app(
        _ id: String,
        _ displayName: String,
        _ accentHex: String,
        _ bundleIdentifiers: [String],
        _ candidatePaths: [String]
    ) -> DockApp {
        DockApp(
            id: id,
            displayName: displayName,
            accentHex: accentHex,
            bundleIdentifiers: bundleIdentifiers,
            candidatePaths: candidatePaths,
            executablePath: nil,
            enabled: false,
            isCustom: false
        )
    }
}
