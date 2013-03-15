<?xml version="1.0" encoding="iso-8859-1"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
    <xsl:template match="/">
        <html>
            <head>
                <title>
                    <xsl:value-of select="rss/channel/title"/> RSS Feed
                </title>
                <style>
                    body {
                    font-family: "Segoe UI", "Lucida Grande", Verdana;
                    background: white;
                    color: #121212;
                    }
                    body > h1 {
                        margin: 0 0 0.1em 0;
                        padding: 0 0 0.1em 0;
                        border-bottom: 1px solid #f1f1f1;
                    }
                    body > h2 {
                        margin: 0;
                        padding: 0;
                        font-size: 100%;
                        font-weight: normal;
                        color: #c0c0c0;
                    }
                    body > img.Logo
                    {
                        position: absolute;
                        top: 0;
                        right: 0;
                        background: white;
                        margin: 4px;
                        padding: 0 0 16px 24px;
                    }
                </style>
            </head>
            <body>
                <h1>
                    <xsl:value-of select="rss/channel/title"/>
                </h1>
                <h2><xsl:value-of select="rss/channel/description"/></h2>
                <img class="Logo" src="{rss/channel/image/url}" />
                <xsl:for-each select="rss/channel/item">
                    <div class="article">
                        <h2>
                            <a href="{link}" rel="bookmark">
                                <xsl:value-of select="title"/>
                            </a>
                        </h2>
                        <xsl:value-of select="description" disable-output-escaping="yes"/>
                    </div>
                </xsl:for-each>
            </body>
        </html>
    </xsl:template>
</xsl:stylesheet>