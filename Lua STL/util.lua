-- Oxide - Lua Standard Library
-- util.lua - Util library


util = {}

function util.ArrayToTable( arr )
	local tbl = {}
	for i=0, arr.Length do
		tbl[ i + 1 ] = arr[ i ]
	end
	return tbl
end
function util.ArrayFromTable( typ, tbl, sz )
	local arr = newarray( typ, sz or #tbl )
	for i=1, #tbl do
		arr[i - 1] = tbl[i]
	end
	return arr
end

function util.GetStaticMethod( typ, name )
	typ = typesystem.TypeFromMetatype( typ )
	tmp = nil
	if (type( typ ) == "table") then
		error( "Tried to get static method of invalid type! (Method name was '" .. name .. "')" )
		return nil
	end
	local cnt = cs.requeststatic( "tmp", typ, name )
	if (cnt == 0) then return end
	local func = tmp
	tmp = nil
	return func
end

function util.GetStaticPropertyGetter( typ, name )
	typ = typesystem.TypeFromMetatype( typ )
	tmp = nil
	cs.requeststaticproperty( "tmp", typ, name )
	if (not tmp) then return end
	local theproperty = tmp
	tmp = nil
	local function Getter()
		return cs.readproperty( theproperty, nil )
	end
	return Getter
end
function util.GetPropertyGetter( typ, name, ulongtouint )
	typ = typesystem.TypeFromMetatype( typ )
	tmp = nil
	cs.requestproperty( "tmp", typ, name )
	if (not tmp) then return end
	local theproperty = tmp
	tmp = nil
	local Getter
	if (ulongtouint) then
		if (ulongtouint == "string") then
			function Getter( obj )
				return cs.readulongpropertyasstring( theproperty, obj )
			end
		else
			function Getter( obj )
				return cs.readulongpropertyasuint( theproperty, obj )
			end
		end
	else
		function Getter( obj )
			return cs.readproperty( theproperty, obj )
		end
	end
	return Getter
end
function util.GetStaticFieldGetter( typ, name, ulongtouint )
	typ = typesystem.TypeFromMetatype( typ )
	tmp = nil
	cs.requeststaticfield( "tmp", typ, name )
	if (not tmp) then return end
	local thefield = tmp
	tmp = nil
	local Getter
	if (ulongtouint) then
		function Getter()
			return cs.readulongfieldasuint( thefield, nil )
		end
	else
		function Getter()
			return cs.readfield( thefield, nil )
		end
	end
	return Getter
end
function util.GetFieldGetter( typ, name, ulongtouint, cast )
	typ = typesystem.TypeFromMetatype( typ )
	tmp = nil
	cs.requestfield( "tmp", typ, name )
	if (not tmp) then return end
	local thefield = tmp
	tmp = nil
	local Getter
	if (ulongtouint) then
		if (ulongtouint == "string") then
			function Getter( obj )
				return cs.readulongfieldasstring( thefield, obj )
			end
		else
			function Getter( obj )
				return cs.readulongfieldasuint( thefield, obj )
			end
		end
	else
		if (cast) then
			cast = typesystem.TypeFromMetatype( cast )
			function Getter( obj )
				return cs.castreadfield( thefield, cast, obj )
			end
		else
			function Getter( obj )
				return cs.readfield( thefield, cast, obj )
			end
		end
	end
	return Getter
end
function util.FindOverloadedMethod( typ, name, bindingflags, typelist )
	typ = typesystem.TypeFromMetatype( typ )
	local methods = typ:GetMethods( bindingflags )
	if (methods.Length == 0) then
		error( "Tried to find overloaded method '" .. name .. "' on type '" .. typ.Name .. "', no candidates found!" )
		return
	end
	local overload = util.FindOverload( methods, typelist, name )
	if (not overload) then
		error( "Tried to find overloaded method '" .. name .. "' on type '" .. typ.Name .. "', specific overload not found!" )
		return
	end
	return overload
end
function util.FindOverload( arr, typelist, name )
	for i=1, arr.Length do
		local methodinfo = arr[i - 1]
		--print( "TESTING: " .. tostring( methodinfo ) )
		if (name and methodinfo.Name == name) then
			--print( "TESTING" )
			local plist = methodinfo:GetParameters()
			local found = true
			for j=1, plist.Length do
				local paraminfo = plist[j - 1]
				local othertype = typesystem.TypeFromMetatype( typelist[j] )
				if (paraminfo.ParameterType ~= othertype) then
					found = false
					break
				end
			end
			if (found) then return methodinfo end
		end
	end
end

local function TrimEnd( str, char )
	local trimoff = 0
	for i=str:len(), 1, -1 do
		local c = str:sub( i, 1 )
		if (c ~= char) then break end
		trimoff = trimoff + 1
	end
	return str:sub( 1, str:len() - trimoff )
end
function util.QuoteSafe( str )
	str = str:gsub( "\"", "\\\"" )
	str = TrimEnd( str, "\\" )
	return str
end

function util.GetDatafile( name )
	return cs.getdatafile( name )
end

function util.ReportError( err )
	--print( type( err ) )
	if (type( err ) == "string") then
		error( err )
	else
		--if (err.Message and err.Source and err.StackTrace) then
			error( err.Message .. " (@" .. err.Source .. ")" )
			error( err.StackTrace )
			if (err.InnerException) then
				util.ReportError( err.InnerException )
			end
		--else
			--error( tostring( err ) )
		--end
	end
end