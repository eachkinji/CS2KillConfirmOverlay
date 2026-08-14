-- sound.lua for csol4 (CSOL 10-kill voice pack)
-- Voice files follow the CSOL native names:
--   1-kill group: Cantbelive / Crazy / Excellent / Firstkill / Incredible
--   2-10 kill:    Doublekill ... Outofworld
--   specials:     Headshot, Humililation / Ohno (knife), Revenge, Assist
-- ctx.voice_picks[name] = "random" or a specific file name (widget advanced settings).
-- ctx.special_voice_priority = false (default) -> streak voice beats headshot/knife voice.

local variants = {
    ["1"] = { "Cantbelive.wav", "Crazy.wav", "Excellent.wav", "Firstkill.wav", "Incredible.wav" },
    ["2"] = { "Doublekill.wav" },
    ["3"] = { "Triplekill.wav" },
    ["4"] = { "Multikill.wav", "Multikill_ch.wav" },
    ["5"] = { "Megakill.wav" },
    ["6"] = { "Rampage.wav" },
    ["7"] = { "Monsterkill.wav" },
    ["8"] = { "Godlike.wav" },
    ["9"] = { "Outofworld.wav" },
    ["10"] = { "Ohgod.wav" },
    ["headshot"] = { "Headshot.wav" },
    ["knife"] = { "Humililation.wav", "Ohno.wav" },
    ["first"] = { "Firstkill.wav" },
    ["last"] = { "Revenge.wav" },
    ["assist"] = { "Assist.wav" },
}

function get_sounds(ctx)
    local sounds = {}
    local base = ctx.base_dir .. "/"

    local function pick(name)
        local pool = variants[name]
        if not pool then
            return nil
        end

        local picks = ctx.voice_picks
        local choice = picks and picks[name]
        if choice and choice ~= "random" then
            for i = 1, #pool do
                if pool[i] == choice then
                    return choice
                end
            end
        end

        return pool[math.random(#pool)]
    end

    local function add(name)
        local file = pick(name)
        if file then
            table.insert(sounds, base .. file)
        end
    end

    if ctx.is_first_kill then
        add("first")
        return sounds
    end

    if ctx.is_last_kill then
        add("last")
        return sounds
    end

    if ctx.is_assist then
        add("assist")
        return sounds
    end

    local special_first = ctx.special_voice_priority
    if special_first == nil then
        special_first = false
    end

    if special_first then
        if ctx.is_knife_kill then
            add("knife")
            return sounds
        end
        if ctx.is_headshot then
            add("headshot")
            return sounds
        end
    end

    if ctx.play_main_audio and ctx.kill_count >= 1 then
        local voiced = math.min(ctx.kill_count, 10)
        add(tostring(voiced))
        return sounds
    end

    if not special_first then
        if ctx.is_knife_kill then
            add("knife")
            return sounds
        end
        if ctx.is_headshot then
            add("headshot")
            return sounds
        end
    end

    return sounds
end
