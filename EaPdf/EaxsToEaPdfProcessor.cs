﻿using Microsoft.Extensions.Logging;
using UIUCLibrary.EaPdf.Helpers;
using UIUCLibrary.EaPdf.Helpers.Pdf;

namespace UIUCLibrary.EaPdf
{
    public class EaxsToEaPdfProcessor
    {

        public const string EABCC = "eabcc";
        public const string EABCC_NS = "http://emailarchivesgrant.library.illinois.edu/ns/";

        private readonly ILogger _logger;
        private readonly IXsltTransformer _xslt;
        private readonly IXslFoTransformer _xslfo;
        private readonly IPdfEnhancerFactory _enhancerFactory;

        public EaxsToEaPdfProcessorSettings Settings { get; }

        /// <summary>
        /// Create a processor for converting email xml archive to an EA-PDF file, initializing the logger, converters, and settings
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="settings"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public EaxsToEaPdfProcessor(ILogger<EaxsToEaPdfProcessor> logger, IXsltTransformer xslt, IXslFoTransformer xslfo, IPdfEnhancerFactory enhancerFactory, EaxsToEaPdfProcessorSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            _xslt = xslt ?? throw new ArgumentNullException(nameof(xslt));
            _xslfo = xslfo ?? throw new ArgumentNullException(nameof(xslfo));
            _enhancerFactory = enhancerFactory ?? throw new ArgumentNullException(nameof(enhancerFactory));

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogTrace($"{this.GetType().Name} Created");

        }

        public void ConvertEaxsToPdf(string eaxsFilePath, string pdfFilePath)
        {
            var foFilePath = Path.ChangeExtension(eaxsFilePath, ".fo");

            var eaxsHelpers = new EaxsHelpers(eaxsFilePath);
            //get fonts based on the Unicode scripts used in the text in the EAXS file and the font settings
            var defaultFonts = eaxsHelpers.GetBaseFontsToUse(Settings);

            var xsltParams = new Dictionary<string, object>
            {
                { "fo-processor-version", _xslfo.ProcessorVersion },
                { "SerifFont", defaultFonts.serifFonts },
                { "SansSerifFont", defaultFonts.sansFonts },
                { "MonospaceFont", defaultFonts.monoFonts }
            };

            List<(LogLevel level, string message)> messages = new();

            //first transform the EAXS to FO using XSLT
            var status = _xslt.Transform(eaxsFilePath, Settings.XsltFoFilePath, foFilePath, xsltParams, ref messages);
            foreach (var (level, message) in messages)
            {
                _logger.Log(level, message);
            }

            //if the first transform was successful, transform the FO to PDF using one of the XSL-FO processors
            if (status == 0)
            {
                messages.Clear();

                //do some post processing on the FO file to prevent ligatures and wrap text in font-family
                var foHelper = new XslFoHelpers(foFilePath);
                foHelper.PreventLigatures();
                foHelper.WrapLanguagesInFontFamily( Settings);
                foHelper.SaveFoFile();

                var status2 = _xslfo.Transform(foFilePath, pdfFilePath, ref messages);
                foreach (var (level, message) in messages)
                {
                    _logger.Log(level, message);
                }
                if (status2 != 0)
                {
                    throw new Exception($"FO transformation to PDF failed, status-{status2}; review log details.");
                }
            }
            else
            {
                throw new Exception($"EAXS transformation to FO failed, status-{status}; review log details.");
            }

#if !DEBUG
            //Delete the intermediate FO file
            File.Delete(foFilePath);
#endif

#if DEBUG
            //save intermediate version of the PDF before post processing
            File.Copy(pdfFilePath, Path.ChangeExtension(pdfFilePath, "pre.pdf"), true);
#endif

            //Do some post processing to add metadata
            AddXmp(eaxsFilePath, pdfFilePath);
        }


        private void AddXmp(string eaxsFilePath, string pdfFilePath)
        {
            var tempOutFilePath = Path.ChangeExtension(pdfFilePath, "out.pdf");

            var dparts = GetXmpMetadataForMessages(eaxsFilePath);
            var docXmp = GetRootXmpForAccount(eaxsFilePath);

            //add docXmp to the DPart root node
            dparts.DpmXmpString = docXmp;

            using var enhancer = _enhancerFactory.Create(_logger, pdfFilePath, tempOutFilePath);

            enhancer.AddXmpToDParts(dparts); //Associate XMP with the PDF DPart of the message

            //dispose of the enhancer to make sure files are closed
            enhancer.Dispose();

            //if all is well, move the temp file over the top of the original
            var pdfFi = new FileInfo(pdfFilePath);
            var tempFi = new FileInfo(tempOutFilePath);

            if (tempFi.Exists && tempFi.Length >= pdfFi.Length)
            {
                File.Move(tempOutFilePath, pdfFilePath, true);
            }
        }


        /// <summary>
        /// Return the root DPart node for all the folders and messages in the EAXS file.
        /// </summary>
        /// <returns></returns>
        private DPartInternalNode GetXmpMetadataForMessages(string eaxsFilePath)
        {
            DPartInternalNode ret = new();

            var xmpFilePath = Path.ChangeExtension(eaxsFilePath, ".xmp");

            List<(LogLevel level, string message)> messages = new();
            var status = _xslt.Transform(eaxsFilePath, Settings.XsltXmpFilePath, xmpFilePath, null, ref messages);
            foreach (var (level, message) in messages)
            {
                _logger.Log(level, message);
            }

            if (status == 0)
            {
                ret.DParts.Add(DPartNode.Create(ret, xmpFilePath));
            }
            else
            {
                throw new Exception("EAXS transformation to XMP failed; review log details.");
            }

#if !DEBUG
            //Delete the intermediate XMP file
            File.Delete(xmpFilePath);
#endif

            return ret;
        }

        private string GetRootXmpForAccount(string eaxsFilePath)
        {
            string ret;

            Dictionary<string, object> parms = new();

            parms.Add("producer", GetType().Namespace ?? "UIUCLibrary");


            var xmpFilePath = Path.ChangeExtension(eaxsFilePath, ".xmp");

            List<(LogLevel level, string message)> messages = new();
            var status = _xslt.Transform(eaxsFilePath, Settings.XsltRootXmpFilePath, xmpFilePath, parms, ref messages);
            foreach (var (level, message) in messages)
            {
                _logger.Log(level, message);
            }

            if (status == 0)
            {
                ret = File.ReadAllText(xmpFilePath);
            }
            else
            {
                throw new Exception("EAXS transformation to XMP failed; review log details.");
            }

#if !DEBUG
            //Delete the intermediate XMP file
            File.Delete(xmpFilePath);
#endif

            return ret;
        }

    }
}
