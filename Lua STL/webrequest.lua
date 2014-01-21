-- Oxide - Lua Standard Library
-- webrequest.lua - webrequest library


webrequest = {}

function webrequest.Send( url, callback )
	return cs.sendwebrequest( url, callback )
end