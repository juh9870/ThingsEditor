import.lazy "./a2".as("a2")

provider = function()
    return 1
end function

consumer = function()
    return a2.cat
end function