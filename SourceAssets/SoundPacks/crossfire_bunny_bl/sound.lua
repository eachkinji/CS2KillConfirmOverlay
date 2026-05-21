-- sound.lua for crossfire_bunny_bl
-- Has: 2.wav through 8.wav, headshot.wav, knife.wav, grenade.wav
-- grenade.wav has priority for first kill and last kill.

function get_sounds(ctx)
    local sounds = {}
    local base = "sounds/" .. ctx.preset_name .. "/"

    if ctx.is_first_kill or ctx.is_last_kill then
        table.insert(sounds, base .. "grenade.wav")
        return sounds
    end

    if ctx.play_main_audio and ctx.kill_count >= 2 then
        local voiced_kill_count = math.min(ctx.kill_count, 8)
        table.insert(sounds, base .. voiced_kill_count .. ".wav")
    elseif ctx.is_knife_kill then
        table.insert(sounds, base .. "knife.wav")
    elseif ctx.is_headshot then
        table.insert(sounds, base .. "headshot.wav")
    end

    return sounds
end
