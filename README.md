# AADBDT – Photo Upload and Browsing Application 

This project is a web application for uploading and browsing photos, implemented as part of the AADBDT course project.  
The app supports different user roles (anonymous, registered, administrator), package-based limits (FREE, PRO, GOLD), and basic image processing during upload and download.

## Functional requirements

### User types and authentication

- Supports three user types:
  - Anonymous user
  - Registered user
  - Administrator
- User registration with:
  - Local account (email + password)
  - External providers (e.g. Google, GitHub), where available in configuration
- During registration, the user must choose one of the packages: FREE, PRO, or GOLD.
- Each package defines limits such as:
  - Maximum daily upload count
  - Maximum allowed size per photo
  - (Optionally) total storage / other constraints

Registered users can:

- Sign in using local credentials or an external provider.
- Sign out safely and return to the public part of the application.

### Packages and usage tracking

- The current package (FREE, PRO, GOLD) is stored with the user.
- Users can:
  - Track current consumption: daily upload count and relevant limits.
  - View a **Usage** page showing:
    - Current package
    - Daily upload count
    - Daily upload limit for the chosen package
    - Total number of uploaded photos
    - Total storage used
- Users can change the package once per day:
  - A change can be requested on the Usage page.
  - The new package becomes active starting from the **next day**.
  - Only one package change per day is allowed; further attempts are rejected with a clear message.

### Photo upload and processing

When uploading a photo, registered users can:

- Upload an image file.
- Set one or more **hashtags**.
- Provide a **description**.
- Choose basic processing options before saving, such as:
  - Resize (e.g. smaller dimensions / thumbnail style)
  - Output format (e.g. PNG, JPG, BMP)

Package limits (for example, daily uploads or maximum size) are enforced during upload.

### Browsing and viewing photos

- All uploaded photos can be browsed.
- Anonymous users:
  - Can browse and search photos.
  - Cannot upload or modify any photo.
- Registered users:
  - Can upload new photos.
  - Can edit **description** and **hashtags** of their own photos.
- The home or main browse page can show, by default:
  - Thumbnails of the **10 last uploaded photos**, including:
    - Description
    - Author
    - Upload date and time
    - Hashtags
- Clicking a thumbnail opens the **full photo** view.

### Searching and filtering

Users can search photos using multiple filters:

- Hashtags
- File size range
- Upload date and time range
- Author

Filters can be combined to narrow down the results.

### Downloading photos

- Users can download photos from the application.
- Two modes are supported:
  - Download the **original** photo.
  - Download a **processed** version of the photo with selected filters, for example:
    - Resize
    - Format change (PNG/JPG/BMP)
    - Optional additional filters (e.g. blur/sepia if implemented)

### Administrator capabilities

Administrators can do everything a registered user can, plus:

- Manage users:
  - View list of users
  - Modify user profiles
  - Change user packages
- View user actions and statistics:
  - See basic usage and activity information per user.
- Manage images:
  - View and manage photos for any user
  - Remove or adjust problematic content where needed

## Nonfunctional requirements

### Logging

- Every significant action is logged with:
  - **Who** performed the action (user identity or anonymous)
  - **When** it happened (timestamp)
  - **What** operation was performed (e.g. upload, download, package change, edit, delete)

This provides a simple audit trail for application usage.

### Storage configuration

- Photo storage is configurable:
  - Local file system










