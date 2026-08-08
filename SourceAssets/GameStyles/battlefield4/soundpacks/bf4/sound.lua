function get_sounds(ctx)
    local sounds = {}
    local base = ctx.base_dir .. "/"

    if ctx.money_reward and ctx.money_reward > 0 then
        table.insert(sounds, base .. "score.wav")
    end

    return sounds
end
