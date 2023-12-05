const storage = multer.diskStorage({
    destination: (req, file, cb) => {
      const folder = determineUploadFolder(file); // Determine the folder dynamically based on the file
      cb(null, '../public/img/corporate-events');
      console.log('File folder destination: '+folder)
    },
    filename: (req, file, cb) => {
      console.log(file);
      const folder = determineUploadFolder(file); // Determine the folder dynamically based on the file
      const filePath = path.join(`${folder}`, Date.now() + path.extname(file.originalname));
      cb(null, filePath);
      console.log('File name: '+ filePath)
    }
  });