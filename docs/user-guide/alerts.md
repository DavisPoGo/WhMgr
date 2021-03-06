# Alerts
Alerts depict how Discord embed messages are formatted. Customization is endless.  

`<name>` - Replacement placeholders.  
`<#condition></condition>` - Conditional replacements.  

**Replacement Placeholders**
Placeholders are used to build a template (similar to [mustache](https://mustache.github.io/)) which are replaced with real values from incoming webhooks and used to send outgoing Discord messages.  

**Conditional replacements**  
Enable the ability to only show something if the conditional value evaluates to `true`. A prime example would be if the Pokemon is near a Pokestop, to include the Pokestop name and image. Below is an example of it:  
```
<#near_pokestop>**Near Pokestop:** [<pokestop_name>](<pokestop_url>)<br></near_pokestop>
```  

`<pokestop_name>` - Replaced by the name of the nearby Pokestop.  
`<pokestop_url>` - Replaced by the image url of the nearby Pokestop.  
`<br>` - Replaced with a new line break to preserve readability and formatting.  

For a list of available dynamic text substitution/replacement options check out the [DTS](user-guide/dts/) pages.  


### Alert Message Structures
```js
{
    "pokemon": {
        "avatarUrl": "",
        "content": "<pkmn_name> <form><gender> <iv> (<atk_iv>/<def_iv>/<sta_iv>) L<lvl><br>**Despawn:** <despawn_time> (<time_left> left)<br>**Details:** CP: <cp> IV: <iv> LV: <lvl><br>**Types:** <types_emoji> | **Size:** <size><#has_weather> | <weather_emoji><#is_weather_boosted> (Boosted)</is_weather_boosted></has_weather><br>**Moveset:** <moveset><br><#near_pokestop>**Near Pokestop:** [<pokestop_name>](<pokestop_url>)<br></near_pokestop><#is_ditto>**Catch Pokemon:** <original_pkmn_name><br></is_ditto><#is_pvp><br><pvp_stats></is_pvp>**[[Google Maps](<gmaps_url>)] [[Apple Maps](<applemaps_url>)] [[Waze Maps](<wazemaps_url>)]**",
        "iconUrl": "",
        "title": "<geofence>",
        "url": "<gmaps_url>",
        "username": "<form> <pkmn_name><gender>",
        "imageUrl": "<tilemaps_url>"
    },
    "pokemonMissingStats": {
        "avatarUrl": "",
        "content": "<pkmn_name> <form><gender><br>**Despawn:** <despawn_time> (<time_left> left)<despawn_time_verified><br>**Types:** <types_emoji><br><#near_pokestop>**Near Pokestop:** [<pokestop_name>](<pokestop_url>)<br></near_pokestop>**[[Google Maps](<gmaps_url>)] [[Apple Maps](<applemaps_url>)] [[Waze Maps](<wazemaps_url>)]**",
        "iconUrl": "",
        "title": "<geofence>",
        "url": "<gmaps_url>",
        "username": "<form> <pkmn_name><gender>",
        "imageUrl": "<tilemaps_url>"
    },
    "gyms": {
        "avatarUrl": "",
        "content": "<#team_changed>Gym changed from <old_gym_team_emoji> <old_gym_team> to <gym_team_emoji> <gym_team><br></team_changed><#in_battle>Gym is under attack!<br></in_battle>**Slots Available:** <slots_available><br><#is_ex><ex_gym_emoji> Gym!</is_ex>**[[Google Maps](<gmaps_url>)] [[Apple Maps](<applemaps_url>)] [[Waze Maps](<wazemaps_url>)]**",
        "iconUrl": "",
        "title": "<geofence>: <gym_name>",
        "url": "<gmaps_url>",
        "username": "<gym_name>",
        "imageUrl": "<tilemaps_url>"
    },
    "raids": {
        "avatarUrl": "",
        "content": "<pkmn_name> Raid Ends: <end_time> (<end_time_left> left)<br>**Perfect CP:** <perfect_cp> / :white_sun_rain_cloud: <perfect_cp_boosted><br>**Worst CP:** <worst_cp> / :white_sun_rain_cloud: <worst_cp_boosted><br>**Types:** <types_emoji> | **Level:** <lvl> | **Team:** <team_emoji><br>**Moveset:** <moveset><br>**Weaknesses:** <weaknesses_emoji><br><#is_ex><ex_emoji> Gym!<br></is_ex>**[[Google Maps](<gmaps_url>)] [[Apple Maps](<applemaps_url>)] [[Waze Maps](<wazemaps_url>)]**",
        "iconUrl": "",
        "title": "<geofence>: <gym_name>",
        "url": "<gmaps_url>",
        "username": "<pkmn_form> <pkmn_name> Raid",
        "imageUrl": "<tilemaps_url>"
    },
    "eggs": {
        "avatarUrl": "",
        "content": "Hatches: <start_time> (<start_time_left>)<br>**Ends:** <end_time> (<end_time_left> left)<br>**Team:** <team_emoji><br><#is_ex><ex_emoji> Gym!<br></is_ex>**[[Google Maps](<gmaps_url>)] [[Apple Maps](<applemaps_url>)] [[Waze Maps](<wazemaps_url>)]**",
        "iconUrl": "",
        "title": "<geofence>: <gym_name>",
        "url": "<gmaps_url>",
        "username": "Level <lvl> Egg",
        "imageUrl": "<tilemaps_url>"
    },
    "pokestops": {
        "avatarUrl": "",
        "content": "<#has_lure>**Lure Expires** <lure_expire_time> (<lure_expire_time_left> left)<br>**Lure Type:** <lure_type><br></has_lure><#has_invasion>**Expires:** <invasion_expire_time> (<invasion_expire_time_left> left)<br>**Type:** <grunt_type_emoji> | **Gender:** <grunt_gender><br><invasion_encounters><br></has_invasion>**[[Google Maps](<gmaps_url>)] [[Apple Maps](<applemaps_url>)] [[Waze Maps](<wazemaps_url>)]**",
        "iconUrl": "<pokestop_url>",
        "title": "<geofence>: <pokestop_name>",
        "url": "<gmaps_url>",
        "username": "<pokestop_name>",
        "imageUrl": "<tilemaps_url>"
    },
    "quests": {
        "avatarUrl": "<quest_reward_img_url>",
        "content": "**Quest:** <quest_task><br><#has_quest_conditions>**Condition(s):** <quest_conditions><br></has_quest_conditions>**Reward:** <quest_reward><br>**[[Google Maps](<gmaps_url>)] [[Apple Maps](<applemaps_url>)] [[Waze Maps](<wazemaps_url>)]**",
        "iconUrl": "<pokestop_url>",
        "title": "<geofence>: <pokestop_name>",
        "url": "<gmaps_url>",
        "username": "<quest_task>",
        "imageUrl": "<tilemaps_url>"
    },
    "lures": {
        "avatarUrl": "",
        "content": "<#has_lure>**Lure Expires:** <lure_expire_time> (<lure_expire_time_left> left)<br>**Lure Type:** <lure_type><br></has_lure>**[[Google Maps](<gmaps_url>)] [[Apple Maps](<applemaps_url>)] [[Waze Maps](<wazemaps_url>)]**",
        "iconUrl": "<pokestop_url>",
        "title": "<geofence>: <pokestop_name>",
        "url": "<gmaps_url>",
        "username": "<pokestop_name>",
        "imageUrl": "<tilemaps_url>"
    },
    "invasions": {
        "avatarUrl": "",
        "content": "<#has_invasion>**Expires:** <invasion_expire_time> (<invasion_expire_time_left> left)<br>**Type:** <grunt_type_emoji> | **Gender:** <grunt_gender><br><invasion_encounters><br></has_invasion>**[[Google Maps](<gmaps_url>)] [[Apple Maps](<applemaps_url>)] [[Waze Maps](<wazemaps_url>)]**",
        "iconUrl": "<pokestop_url>",
        "title": "<geofence>: <pokestop_name>",
        "url": "<gmaps_url>",
        "username": "<pokestop_name>",
        "imageUrl": "<tilemaps_url>"
    }
}
```