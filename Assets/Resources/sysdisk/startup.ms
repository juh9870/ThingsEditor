import.ensureFresh false;

// Convert a 0-255 number to a 2-digit hex string
globals.hex2 = function(val)
	result = 0
	digits = "0123456789ABCDEF"
	val = floor(val)
	if val < 0 then return "00"
	if val >= 255 then return "FF"
	return digits[val / 16] + digits[val % 16]
end function

import from("./defaults/color.ms").into(globals).as("color");

globals._apiClassNames = {
}

globals.mapName = function(map)
	if map.hasIndex("_name") then return map._name
	if _apiClassNames.hasIndex(map) then return _apiClassNames[map]
	for kv in globals
		if @kv.value == map then return @kv.key
	end for
	return null
end function

globals.pprintToList = function(outList, obj, prefix="", indent=0, suffix="")
	s = str(@obj)
	if @obj == null then s = "null"
	if @obj isa funcRef then s = "function" + s[8:]
	if s.len + prefix.len + indent < 38 then
		outList.push " "*indent + prefix + s + suffix
		return
	end if
	if indent > 32 then
		outList.push " "*indent + prefix + "..." + suffix
		return
	end if
	if @obj isa list then
		outList.push " "*indent + prefix + "["
		for i in obj.indexes
			pprintToList outList, obj[i], "", indent + 4, "," * (i < obj.len-1)
		end for
		outList.push " "*indent + "]" + suffix
	else if @obj isa map then
		outList.push " "*indent + prefix + "{"
		entries = []; for kv in obj; entries.push kv; end for
		for i in entries.indexes
			kv = entries[i]
			value = null
			if @kv.value isa map then value = mapName(@kv.value)
			if value == null then value = @kv.value
			pprintToList outList, @value, @kv.key + ": ", indent+4, "," * (i < entries.len-1)
		end for
		outList.push " "*indent + "}" + suffix
	else
		outList.push " "*indent + prefix + s + suffix
	end if
end function
