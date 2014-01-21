-- Oxide - Lua Standard Library
-- timer.lua - timer library


timer = {}

function timer.Once( delay, func )
	return cs.newtimer( delay, 1, func )
end
function timer.NextFrame( func )
	return timer.Once( 0, func )
end
function timer.Repeat( delay, arg1, arg2 )
	if (arg2) then
		return cs.newtimer( delay, arg1, arg2 )
	else
		return cs.newtimer( delay, 0, arg1 )
	end
end
function timer.Chain( ... )
	local args = { ... }
	local start = 0
	local timers = {}
	for i=1, #args do
		local arg = args[i]
		if (type( arg ) == "number") then
			start = start + arg
		else
			timers[ #timers + 1 ] = timer.Once( start, arg )
		end
	end
	return timers
end