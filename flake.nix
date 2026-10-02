{
  description = "Terminal music player (senior project) — dev shell";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs = { nixpkgs, ... }:
    let
      pkgs = nixpkgs.legacyPackages.x86_64-linux;
    in {
      devShells.x86_64-linux.default = pkgs.mkShell {
        packages = [ pkgs.dotnet-sdk_10 ];

        # LibVLCSharp dlopens libvlc at runtime; NixOS has no /usr/lib for it to search
        LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath [ pkgs.libvlc ];

        # built binaries (bin/Debug/.../App) look here for the runtime; `dotnet run` doesn't need it
        DOTNET_ROOT = "${pkgs.dotnet-sdk_10}/share/dotnet";

        DOTNET_CLI_TELEMETRY_OPTOUT = "1";
        DOTNET_NOLOGO = "1";
      };
    };
}
