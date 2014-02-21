-- Oxide - Lua Standard Library
-- webrequest.lua - webrequest library


webrequest = {}

function webrequest.Send( url, callback )
	return cs.sendwebrequest( url, callback )
end

function webrequest.Post( url, data, callback )
	return cs.postwebrequest( url, data, callback )
end