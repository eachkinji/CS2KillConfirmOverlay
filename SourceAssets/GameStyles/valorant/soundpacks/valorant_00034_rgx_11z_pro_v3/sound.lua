-- Valorant kill soundpack generated from demo asset pack.
-- Plays tier 1-5, and caps higher kill counts at tier 5.
function get_sounds(ctx)
    local sounds = {}
    local base = ctx.base_dir .. "/"
    if ctx.is_headshot then
        table.insert(sounds, base .. "headshot.wav")
    end

    local sound_num = ctx.kill_count
    if sound_num < 1 then
        sound_num = 1
    end
    if sound_num > 5 then
        sound_num = 5
    end
    table.insert(sounds, base .. tostring(sound_num) .. ".wav")
    return sounds
end
