# ClipboardSync

ClipboardSync is a Windows application that synchronizes clipboard content between multiple computers using a USB flash drive and a KVM switch. It captures clipboard changes, saves the content to the flash drive, and restores it on the target computer when switched via the KVM. 

ClipboardSync ensures that all data types—including **plain text**, **RTF (Rich Text Format)**, **HTML**, and **file drops**—are preserved, retaining formatting and file structures across devices.

> **Note:** This project is currently under construction.

## Features

- **Comprehensive Clipboard Data Handling**: Supports plain text, formatted text (RTF, HTML), and file drops, ensuring data fidelity during transfers.
- **Automatic Clipboard Updates**: Monitors clipboard changes in real-time and saves them to `clipboard_data.json` on the USB flash drive.
- **Flash Drive Syncing**: Loads clipboard content from the flash drive on the new computer upon switching.
- **Data Replacement**: Automatically deletes old clipboard data when new content is copied, ensuring efficient storage management.
- **Progress Tracking**: Includes a progress bar for file drop synchronization, displaying the status of file copying.

## How It Works

1. **Clipboard Monitoring**: The app uses event-driven clipboard monitoring to detect changes.
2. **Data Serialization**: Clipboard content is serialized into a structured JSON format (`clipboard_data.json`) and stored on the USB flash drive. RTF and HTML formats are saved in separate files if applicable.
3. **Data Restoration**: When the USB flash drive is detected on a new machine, the application deserializes the stored data and restores it to the clipboard.
4. **File Drop Handling**: If clipboard content contains file paths, the application copies the files to the USB flash drive and restores them to the clipboard as file drops.

### File Details

- **clipboard_data.json**: Stores metadata about the clipboard content type and associated data.
- **clipboard_data.rtf** and **clipboard_data.html**: Store rich text and HTML content when applicable.
- **Files**: If clipboard data includes file drops, the application copies the files directly to the USB flash drive.

## Limitations

- ClipboardSync only works with a connected USB flash drive; it does not support network-based clipboard syncing.
- The performance of file synchronization depends on the USB drive's read/write speed.

## Contributing

Contributions are welcome! If you encounter issues or have suggestions for improvement, please open an issue or submit a pull request.

## License

ClipboardSync is licensed under the [GNU General Public License v3.0](LICENSE).

---

Developed with ❤️ by [Brandon Henness](https://github.com/brandonhenness).
