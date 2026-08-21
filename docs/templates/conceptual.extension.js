// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

var months = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December'
]

function normalizePublicationDate(value) {
  if (value === undefined || value === null || value === '') {
    return null
  }

  var text = String(value)
  var match = /^(\d{4})-(\d{2})-(\d{2})/.exec(text)

  if (!match) {
    return {
      iso: text,
      display: text
    }
  }

  var month = Number(match[2])
  var day = Number(match[3])

  return {
    iso: match[1] + '-' + match[2] + '-' + match[3],
    display: months[month - 1] + ' ' + day + ', ' + match[1]
  }
}

function firstText() {
  for (var index = 0; index < arguments.length; index++) {
    var value = arguments[index]

    if (value !== undefined && value !== null && String(value).trim() !== '') {
      return String(value).trim()
    }
  }

  return null
}

function normalizeCanonicalBaseUrl(value) {
  var baseUrl = firstText(value)

  if (!baseUrl) {
    return null
  }

  return baseUrl.replace(/\/+$/, '') + '/'
}

function buildCanonicalUrl(baseUrl, outputPath) {
  var siteRoot = normalizeCanonicalBaseUrl(baseUrl)
  var path = firstText(outputPath)

  if (!siteRoot || !path) {
    return null
  }

  path = path
    .replace(/\\/g, '/')
    .replace(/^\.\//, '')
    .replace(/^\/+/, '')

  // GitHub Pages serves index.html through both explicit and directory-style URLs.
  // Prefer the directory form for indexes while leaving ordinary article .html paths intact.
  if (path.toLowerCase() === 'index.html') {
    return siteRoot
  }

  if (/\/index\.html$/i.test(path)) {
    return siteRoot + path.slice(0, -'index.html'.length)
  }

  return siteRoot + path
}

function isTrue(value) {
  return value === true || String(value).toLowerCase() === 'true'
}

function personNodes(value) {
  var values = Array.isArray(value) ? value : [value]
  var nodes = values
    .map(function (item) { return firstText(item) })
    .filter(function (item) { return item !== null })
    .map(function (name) {
      return {
        '@type': 'Person',
        name: name
      }
    })

  if (nodes.length === 1) {
    return nodes[0]
  }

  return nodes
}

function safeJson(value) {
  return JSON.stringify(value)
    .replace(/</g, '\\u003c')
    .replace(/>/g, '\\u003e')
    .replace(/&/g, '\\u0026')
    .replace(/\u2028/g, '\\u2028')
    .replace(/\u2029/g, '\\u2029')
}

function buildStructuredData(model) {
  var siteRoot = normalizeCanonicalBaseUrl(model._canonicalBaseUrl)
  var canonicalUrl = firstText(model._canonicalUrl)

  if (!siteRoot || !canonicalUrl) {
    return null
  }

  var siteName = firstText(model._structuredDataSiteName, model._appName, 'ASI Backbone Learning')
  var siteAlternateName = firstText(model._structuredDataSiteAlternateName)
  var siteDescription = firstText(model._structuredDataSiteDescription)
  var publisherName = firstText(model._structuredDataPublisherName)
  var publisherAlternateName = firstText(model._structuredDataPublisherAlternateName)
  var publisherUrl = firstText(model._structuredDataPublisherUrl)
  var pageTitle = firstText(model.title, siteName)
  var pageDescription = firstText(model.description, model._description, model.summary)
  var websiteId = siteRoot + '#website'
  var publisherId = siteRoot + '#publisher'
  var webPageId = canonicalUrl + '#webpage'
  var graph = []

  var website = {
    '@type': 'WebSite',
    '@id': websiteId,
    url: siteRoot,
    name: siteName
  }

  if (siteAlternateName) {
    website.alternateName = siteAlternateName
  }

  if (siteDescription) {
    website.description = siteDescription
  }

  if (publisherName) {
    website.publisher = { '@id': publisherId }
  }

  graph.push(website)

  if (publisherName) {
    var publisher = {
      '@type': 'Organization',
      '@id': publisherId,
      name: publisherName
    }

    if (publisherAlternateName) {
      publisher.alternateName = publisherAlternateName
    }

    if (publisherUrl) {
      publisher.url = publisherUrl
    }

    graph.push(publisher)
  }

  var webPage = {
    '@type': 'WebPage',
    '@id': webPageId,
    url: canonicalUrl,
    name: pageTitle,
    isPartOf: { '@id': websiteId }
  }

  if (pageDescription) {
    webPage.description = pageDescription
  }

  var isArticle = isTrue(model.feed) && model.author && model.published

  if (isArticle) {
    webPage.mainEntity = { '@id': canonicalUrl + '#article' }
  }

  graph.push(webPage)

  if (isArticle) {
    var article = {
      '@type': 'Article',
      '@id': canonicalUrl + '#article',
      url: canonicalUrl,
      headline: pageTitle,
      mainEntityOfPage: { '@id': webPageId },
      isPartOf: { '@id': websiteId },
      datePublished: model._publicationPublishedIso,
      dateModified: model._publicationUpdatedIso || model._publicationPublishedIso,
      author: personNodes(model.author)
    }

    if (pageDescription) {
      article.description = pageDescription
    }

    if (publisherName) {
      article.publisher = { '@id': publisherId }
    }

    graph.push(article)
  }

  return safeJson({
    '@context': 'https://schema.org',
    '@graph': graph
  })
}

exports.postTransform = function (model) {
  if (!model.description && !model._description && model.summary) {
    model.description = model.summary
  }

  model._canonicalUrl = buildCanonicalUrl(model._canonicalBaseUrl, model._path)

  if (model.author && model.published) {
    var published = normalizePublicationDate(model.published)
    var updated = normalizePublicationDate(model.updated)

    model._hasPublicationMetadata = true
    model._publicationPublishedIso = published.iso
    model._publicationPublishedDisplay = published.display

    if (updated) {
      model._publicationUpdatedIso = updated.iso
      model._publicationUpdatedDisplay = updated.display
    }
  }

  model._structuredDataJson = buildStructuredData(model)

  return model
}
