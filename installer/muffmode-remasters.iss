#define AppName "MuffMode Map Remasters"
#define AppVersion "0.0.0"
#define AppPublisher "MuffMode"

[Setup]
AppId={{A1B94120-8A89-4A79-A21C-70C6E8C5F9D9}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={code:GetDefaultInstallDir}
DisableProgramGroupPage=yes
OutputBaseFilename=MuffMode-Map-Remasters-{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
WizardStyle=modern

[Files]
Source: "..\\finals\\maps\\*.bsp"; DestDir: "{code:GetMapsDir}"; Flags: ignoreversion createallsubdirs
Source: "..\\dist\\updater\\MuffMode-Map-Remasters-Updater.exe"; DestDir: "{app}"; DestName: "MuffMode-Map-Remasters-Updater.exe"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\MuffMode Map Remasters Updater"; Filename: "{app}\MuffMode-Map-Remasters-Updater.exe"

[Registry]
Root: HKCU; Subkey: "Software\MuffMode\MapRemasters\Updater"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\MuffMode\MapRemasters\Updater"; ValueType: dword; ValueName: "AutoLaunch"; ValueData: "0"; Flags: uninsdeletevalue

[Code]
const
  QuakeFolderNameA = 'Quake 2';
  QuakeFolderNameB = 'Quake II';

function FindPosEx(SubStr: string; S: string; Offset: Integer): Integer;
var
  Tail: string;
begin
  if Offset < 1 then Offset := 1;
  Tail := Copy(S, Offset, Length(S) - Offset + 1);
  Result := Pos(SubStr, Tail);
  if Result > 0 then
    Result := Result + Offset - 1;
end;

function NormalizePath(Path: string): string;
begin
  Result := Path;
  StringChangeEx(Result, '/', '\\', True);
  if (Length(Result) > 0) and (Result[Length(Result)] = '\\') then
    Result := Copy(Result, 1, Length(Result) - 1);
end;

function LooksLikePath(Value: string): Boolean;
begin
  Result := (Pos(':\\', Value) > 0) or (Pos(':/', Value) > 0);
end;

function IsQuake2Root(Path: string): Boolean;
var
  exePath: string;
begin
  exePath := AddBackslash(Path) + 'quake2.exe';
  Result := DirExists(Path) and (
    FileExists(exePath) or
    FileExists(AddBackslash(Path) + 'Quake2.exe') or
    DirExists(AddBackslash(Path) + 'baseq2') or
    DirExists(AddBackslash(Path) + 'rerelease')
  );
end;

function ExtractSecondQuoted(Line: string): string;
var
  start1, end1, start2, end2: Integer;
begin
  Result := '';
  start1 := Pos('"', Line);
  if start1 = 0 then Exit;
  end1 := FindPosEx('"', Line, start1 + 1);
  if end1 = 0 then Exit;
  start2 := FindPosEx('"', Line, end1 + 1);
  if start2 = 0 then Exit;
  end2 := FindPosEx('"', Line, start2 + 1);
  if end2 = 0 then Exit;
  Result := Copy(Line, start2 + 1, end2 - start2 - 1);
end;

function FindSteamLibraryGamePath(SteamPath: string): string;
var
  libraryFile: string;
  lines: TArrayOfString;
  i: Integer;
  line, libPath, candidate: string;
begin
  Result := '';
  libraryFile := AddBackslash(SteamPath) + 'steamapps\\libraryfolders.vdf';
  if not LoadStringsFromFile(libraryFile, lines) then Exit;

  for i := 0 to GetArrayLength(lines) - 1 do begin
    line := Trim(lines[i]);
    libPath := '';

    if Pos('"path"', line) > 0 then
      libPath := ExtractSecondQuoted(line)
    else if Pos('"', line) > 0 then
      libPath := ExtractSecondQuoted(line);

    if (libPath <> '') and LooksLikePath(libPath) then begin
      libPath := NormalizePath(libPath);
      candidate := AddBackslash(libPath) + 'steamapps\\common\\' + QuakeFolderNameA;
      if IsQuake2Root(candidate) then begin
        Result := candidate;
        Exit;
      end;
      candidate := AddBackslash(libPath) + 'steamapps\\common\\' + QuakeFolderNameB;
      if IsQuake2Root(candidate) then begin
        Result := candidate;
        Exit;
      end;
    end;
  end;
end;

function FindSteamInstall(): string;
var
  steamPath: string;
  candidate: string;
begin
  Result := '';
  if RegQueryStringValue(HKCU, 'Software\\Valve\\Steam', 'SteamPath', steamPath) or
     RegQueryStringValue(HKCU, 'Software\\Valve\\Steam', 'InstallPath', steamPath) then begin
    steamPath := NormalizePath(steamPath);

    candidate := AddBackslash(steamPath) + 'steamapps\\common\\' + QuakeFolderNameA;
    if IsQuake2Root(candidate) then begin
      Result := candidate;
      Exit;
    end;

    candidate := AddBackslash(steamPath) + 'steamapps\\common\\' + QuakeFolderNameB;
    if IsQuake2Root(candidate) then begin
      Result := candidate;
      Exit;
    end;

    Result := FindSteamLibraryGamePath(steamPath);
  end;
end;

function FindGogInstallFromRegistry(RootKey: Integer; RootPath: string): string;
var
  keys: TArrayOfString;
  i: Integer;
  keyPath, pathValue, nameValue: string;
begin
  Result := '';
  if not RegGetSubkeyNames(RootKey, RootPath, keys) then Exit;

  for i := 0 to GetArrayLength(keys) - 1 do begin
    keyPath := RootPath + '\\' + keys[i];
    if RegQueryStringValue(RootKey, keyPath, 'path', pathValue) then begin
      nameValue := '';
      RegQueryStringValue(RootKey, keyPath, 'gameName', nameValue);
      if nameValue = '' then RegQueryStringValue(RootKey, keyPath, 'GameName', nameValue);
      if nameValue = '' then RegQueryStringValue(RootKey, keyPath, 'DisplayName', nameValue);
      if nameValue = '' then RegQueryStringValue(RootKey, keyPath, 'name', nameValue);

      if (nameValue = '') or (Pos('Quake II', nameValue) > 0) or (Pos('Quake 2', nameValue) > 0) then begin
        pathValue := NormalizePath(pathValue);
        if IsQuake2Root(pathValue) then begin
          Result := pathValue;
          Exit;
        end;
      end;
    end;
  end;
end;

function FindGogInstall(): string;
begin
  Result := FindGogInstallFromRegistry(HKLM, 'SOFTWARE\\GOG.com\\Games');
  if Result <> '' then Exit;
  Result := FindGogInstallFromRegistry(HKLM, 'SOFTWARE\\WOW6432Node\\GOG.com\\Games');
  if Result <> '' then Exit;

  if IsQuake2Root('C:\\GOG Games\\Quake II') then begin
    Result := 'C:\\GOG Games\\Quake II';
    Exit;
  end;

  if IsQuake2Root('C:\\Program Files (x86)\\GOG Galaxy\\Games\\Quake II') then begin
    Result := 'C:\\Program Files (x86)\\GOG Galaxy\\Games\\Quake II';
    Exit;
  end;
end;

function GetJsonValue(Json: string; Key: string): string;
var
  keyPattern: string;
  startPos, quoteStart, quoteEnd: Integer;
begin
  Result := '';
  keyPattern := '"' + Key + '"';
  startPos := Pos(keyPattern, Json);
  if startPos = 0 then Exit;
  startPos := FindPosEx(':', Json, startPos + Length(keyPattern));
  if startPos = 0 then Exit;
  quoteStart := FindPosEx('"', Json, startPos);
  if quoteStart = 0 then Exit;
  quoteEnd := FindPosEx('"', Json, quoteStart + 1);
  if quoteEnd = 0 then Exit;
  Result := Copy(Json, quoteStart + 1, quoteEnd - quoteStart - 1);
end;

function UnescapeJsonPath(Value: string): string;
begin
  Result := Value;
  StringChangeEx(Result, '\\\\', '\\', True);
end;

function FindEosInstall(): string;
var
  manifestDir, fileName, json, displayName, installLocation: string;
  findRec: TFindRec;
begin
  Result := '';
  manifestDir := ExpandConstant('{commonappdata}\\Epic\\EpicGamesLauncher\\Data\\Manifests');
  if not DirExists(manifestDir) then Exit;

  if FindFirst(manifestDir + '\\*.item', findRec) then begin
    repeat
      fileName := manifestDir + '\\' + findRec.Name;
      if LoadStringFromFile(fileName, json) then begin
        displayName := GetJsonValue(json, 'DisplayName');
        if (Pos('Quake II', displayName) > 0) or (Pos('Quake 2', displayName) > 0) then begin
          installLocation := GetJsonValue(json, 'InstallLocation');
          installLocation := NormalizePath(UnescapeJsonPath(installLocation));
          if IsQuake2Root(installLocation) then begin
            Result := installLocation;
            Break;
          end;
        end;
      end;
    until not FindNext(findRec);
    FindClose(findRec);
  end;
end;

function FindBestInstallDir(): string;
begin
  Result := FindSteamInstall();
  if Result <> '' then Exit;
  Result := FindGogInstall();
  if Result <> '' then Exit;
  Result := FindEosInstall();
  if Result <> '' then Exit;

  Result := 'C:\\Games\\Quake 2';
end;

function GetDefaultInstallDir(Param: string): string;
begin
  Result := FindBestInstallDir();
end;

function GetMapsDir(Param: string): string;
var
  rootPath: string;
  rereleaseBase: string;
begin
  rootPath := ExpandConstant('{app}');
  rereleaseBase := AddBackslash(rootPath) + 'rerelease\\baseq2';
  if DirExists(rereleaseBase) then begin
    Result := AddBackslash(rereleaseBase) + 'maps';
  end else begin
    Result := AddBackslash(rootPath) + 'baseq2\\maps';
  end;
end;
