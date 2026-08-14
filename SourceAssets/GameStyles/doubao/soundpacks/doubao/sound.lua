function get_sounds(ctx)
    local sounds = {}
    if not ctx.play_main_audio or ctx.kill_count < 1 then
        return sounds
    end

    local voiced_kill_count = math.min(ctx.kill_count, 5)
    table.insert(sounds, ctx.base_dir .. "/" .. voiced_kill_count .. "kill.wav")
    return sounds
end
