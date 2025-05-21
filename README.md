# ShareU - Secure Local Network File Transfer

ShareU is a modern, user-friendly file transfer application that allows you to securely share files over your local network. With its intuitive interface and QR code functionality, sharing files between devices has never been easier.

![ShareU Screenshot](screenshot.png)

## Features

- 🚀 **Easy File Sharing**: Share files instantly over your local network
- 📱 **QR Code Support**: Scan QR code with mobile devices for quick access
- 🔒 **Secure Transfer**: Files are transferred securely within your local network
- 🎨 **Modern UI**: Clean and intuitive user interface
- 📋 **Copy URL**: One-click URL copying for easy sharing
- 📂 **Multi-file Support**: Upload and manage multiple files
- 🗑️ **File Management**: Delete files directly from the interface
- 🔄 **Real-time Status**: Clear status indicators for server state

## Requirements

- Windows 10 or later
- .NET 6.0 or later
- Local network connection

## Installation

1. Download the latest release from the [Releases](https://github.com/yourusername/ShareU/releases) page
2. Extract the ZIP file to your desired location
3. Run `ShareU.exe`

## Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/ShareU.git
   ```

2. Navigate to the project directory:
   ```bash
   cd ShareU
   ```

3. Restore dependencies:
   ```bash
   dotnet restore
   ```

4. Build the project:
   ```bash
   dotnet build
   ```

5. Run the application:
   ```bash
   dotnet run
   ```

## Usage

1. Launch ShareU
2. Click "Start Share" to begin the file sharing server
3. Choose one of the following methods to access the file sharing interface:
   - Scan the QR code with your mobile device
   - Copy the URL and open it in any web browser
4. Upload files by dragging and dropping or using the file picker
5. Download files by clicking on them in the file list
6. Delete files using the delete button next to each file
7. Click "Stop Share" when you're done

## Security

- Files are only accessible within your local network
- No data is stored on external servers
- Automatic firewall rule management for secure connections
- Files are served over HTTP for easy access

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [QRCoder](https://github.com/codebude/QRCoder) for QR code generation
- [Material Design Icons](https://materialdesignicons.com/) for UI icons
- [Modern UI Design](https://www.modernui.com/) for design inspiration

---

Made with ❤️ by Abdelrhaman Ahmed
