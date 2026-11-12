$progIds = @("Gcad.Application", "GstarCAD.Application", "Gcad.Application.24")
foreach ($id in $progIds) {
    try {
        $app = [Runtime.InteropServices.Marshal]::GetActiveObject($id)
        Write-Output "PROGID=$id"
        $doc = $app.ActiveDocument
        foreach ($name in @("LASTPROMPT", "CMDNAMES", "SECURELOAD", "TRUSTEDPATHS", "FILEDIA")) {
            try {
                Write-Output "$name=$($doc.GetVariable($name))"
            }
            catch {
                Write-Output "$name=<unavailable>"
            }
        }
        break
    }
    catch {
        Write-Output "$id : $($_.Exception.Message)"
    }
}
