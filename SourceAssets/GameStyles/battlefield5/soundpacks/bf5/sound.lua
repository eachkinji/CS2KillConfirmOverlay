function get_sounds(ctx)
    local sounds = {}
    local base = ctx.base_dir .. "/"
    if ctx.is_headshot then
        table.insert(sounds, base .. "headshot.wav")
    elseif ctx.play_main_audio then
        table.insert(sounds, base .. "common.wav")
    end
    return sounds
end
