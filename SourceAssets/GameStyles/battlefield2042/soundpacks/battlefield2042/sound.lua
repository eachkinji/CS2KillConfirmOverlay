function get_sounds(ctx)
    local sounds = {}
    local base = ctx.base_dir .. "/"

    if ctx.event_kind == "round_win" or ctx.event_kind == "round_loss" then
        return sounds
    end

    if ctx.is_assist then
        return sounds
    end

    if ctx.is_headshot then
        table.insert(sounds, base .. "headshot.wav")
    elseif ctx.play_main_audio then
        table.insert(sounds, base .. "normal.wav")
    end

    return sounds
end
