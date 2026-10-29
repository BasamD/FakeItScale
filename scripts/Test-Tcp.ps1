param([string]$Server = '127.0.0.1', [int]$Port = 10001)
$ErrorActionPreference = 'Stop'
$client = [System.Net.Sockets.TcpClient]::new()
try {
    $client.Connect($Server, $Port)
    $stream = $client.GetStream()
    $stream.ReadTimeout = 10000
    for ($i = 0; $i -lt 21; $i++) {
        $bytes = [byte[]]::new(14)
        $offset = 0
        while ($offset -lt 14) {
            $read = $stream.Read($bytes, $offset, 14 - $offset)
            if ($read -eq 0) { throw 'Disconnected before a complete frame.' }
            $offset += $read
        }
        $weight = 20000 - ([math]::Floor($i / 10) * 20)
        $expected = [System.Text.Encoding]::ASCII.GetBytes(([char]2).ToString() + '   ' + $weight.ToString('00000') + "LG `r`n")
        if ([BitConverter]::ToString($bytes) -ne [BitConverter]::ToString($expected)) {
            throw "Frame $($i+1) mismatch: $([BitConverter]::ToString($bytes))"
        }
        Write-Output "Frame $($i+1): $weight lb OK"
    }
    Write-Output 'PASS: 21 exact frames and two ten-send hold windows.'
} finally { $client.Dispose() }
