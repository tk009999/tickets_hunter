using System.Text.Json.Nodes;

namespace TicketsHunter.Desktop;

internal static class DefaultConfig
{
    internal static JsonObject Create() => JsonNode.Parse("""
    {
      "homepage": "",
      "browser": "chrome",
      "language": "繁體中文",
      "ticket_number": 1,
      "refresh_datetime": "",
      "ocr_captcha": {
        "enable": true,
        "beta": true,
        "force_submit": false,
        "image_source": "canvas",
        "use_universal": true,
        "path": "assets/model/universal"
      },
      "webdriver_type": "nodriver",
      "date_auto_select": {
        "enable": true,
        "date_keyword": "",
        "mode": "from top to bottom"
      },
      "area_auto_select": {
        "enable": true,
        "mode": "from top to bottom",
        "area_keyword": ""
      },
      "keyword_exclude": "\"輪椅\",\"身障\",\"視線不良\",\"燈柱遮蔽\"",
      "kktix": {
        "auto_press_next_step_button": true,
        "auto_fill_ticket_number": true,
        "max_dwell_time": 90
      },
      "cityline": {},
      "tixcraft": {
        "pass_date_is_sold_out": true,
        "auto_reload_coming_soon_page": true,
        "allow_less_tickets": false
      },
      "contact": {
        "real_name": "",
        "phone": "",
        "credit_card_prefix": ""
      },
      "accounts": {
        "tixcraft_sid": "", "ibonqware": "", "funone_session_cookie": "",
        "fansigo_cookie": "", "fansigo_account": "", "fansigo_password": "",
        "facebook_account": "", "facebook_password": "", "kktix_account": "",
        "kktix_password": "", "fami_account": "", "fami_password": "",
        "cityline_account": "", "cityline_password": "", "urbtix_account": "",
        "urbtix_password": "", "hkticketing_account": "", "hkticketing_password": "",
        "kham_account": "", "kham_password": "", "ticket_account": "",
        "ticket_password": "", "udn_account": "", "udn_password": "",
        "ticketplus_account": "", "ticketplus_password": ""
      },
      "advanced": {
        "play_sound": { "ticket": true, "order": true, "filename": "assets/sounds/ding-dong.wav" },
        "disable_adjacent_seat": false,
        "hide_some_image": false,
        "block_facebook_network": false,
        "headless": false,
        "user_data_dir": "",
        "verbose": true,
        "show_timestamp": true,
        "auto_guess_options": false,
        "user_guess_string": "",
        "discount_code": "",
        "server_port": 16888,
        "remote_url": "\"http://127.0.0.1:16888/\"",
        "auto_reload_page_interval": 5,
        "tixcraft_soft_block_delay": "",
        "reset_browser_interval": 0,
        "proxy_server_port": "",
        "window_size": "900,760",
        "idle_keyword": "", "resume_keyword": "",
        "idle_keyword_second": "", "resume_keyword_second": "",
        "discord_webhook_url": "", "discord_message": "",
        "telegram_bot_token": "", "telegram_chat_id": "", "telegram_message": ""
      },
      "date_auto_fallback": false,
      "area_auto_fallback": false
    }
    """)!.AsObject();
}
