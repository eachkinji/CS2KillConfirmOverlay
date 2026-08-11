function get_sounds(ctx)
    local sounds = {}
    local base = ctx.base_dir .. "/"

    if ctx.money_reward and ctx.money_reward > 0 then
        table.insert(sounds, base .. "score.wav")
    end

    if ctx.event_kind == "round_win" or ctx.event_kind == "round_loss" then
        return sounds
    end

    if ctx.is_assist then
        table.insert(sounds, base .. "assist.wav")
    elseif ctx.is_headshot then
        table.insert(sounds, base .. "hit.wav")
        table.insert(sounds, base .. "headshot.wav")
    elseif ctx.is_knife_kill then
        table.insert(sounds, base .. "crit.wav")
    elseif ctx.play_main_audio then
        table.insert(sounds, base .. "default.wav")
    end

    return sounds
end
