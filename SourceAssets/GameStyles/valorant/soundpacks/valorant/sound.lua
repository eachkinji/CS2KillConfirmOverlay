-- sound.lua for valorant soundpack
-- Has: 1.wav through 5.wav (numbered sounds only, no common/headshot)
-- Logic: Play numbered sound based on kill count (1-5)

function get_sounds(ctx)
    local sounds = {}
    local base = "sounds/" .. ctx.preset_name .. "/"

    -- Play Valorant headshot layer first, then numbered kill tier.
    if ctx.is_headshot then
        table.insert(sounds, base .. "headshot.wav")
    end

    local sound_num = ctx.kill_count
    if sound_num > 5 then
        sound_num = 5
    end

    table.insert(sounds, base .. sound_num .. ".wav")

    return sounds
end
