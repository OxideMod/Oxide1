-- Oxide - Lua Standard Library
-- validate.lua - validate library


validate = {}

local function MatchType( obj, template )
	if (obj == nil) then return template == "any" or template == "nil" end
	local t = type( obj )
	if (t == template) then return true end
	if (t == "function") then return template == "function" end
	if (t == "number") then return template == "number" end
	local typ = typesystem.TypeFromMetatype( template )
	if (obj.GetType) then return obj:GetType() == typ end
	return false
end

local function TypeToString( template )
	if (type( template ) == "string") then
		return template
	else
		local typ = typesystem.TypeFromMetatype( template )
		return typ.FullName
	end
end

function validate.Args( funcname, template, ... )
	local args = { ... }
	for i=1, #template do
		if (not MatchType( args[i], template[i] )) then
			local tbl = debug.getinfo( 3, "Sl" )
			error( tbl.short_src .. ":" .. tbl.currentline .. " - Invalid argument " .. i .. " to " .. funcname .. " (expecting " .. TypeToString( template[i] ) .. ", got " .. type( args[i] ) .. ")" )
			return false
		end
	end
	return true
end